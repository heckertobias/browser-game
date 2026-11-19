using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>015 T4: Slash-Commands im GameLoop, ohne Netz und DB.</summary>
public sealed class GameLoopCommandTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private const double Far = 500; // weit außerhalb des SpawnRadius

    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build(), new FakeClock(Start));

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static List<ServerMessage.PayloadOneofCase> Payloads(IEnumerable<ServerMessage> messages) => [.. messages.Select(m => m.PayloadCase)];

    private static List<ChatHistoryEntry> ReadQueue(GameLoop game)
    {
        var entries = new List<ChatHistoryEntry>();
        while (game.ChatToSave.TryRead(out var entry))
        {
            entries.Add(entry);
        }

        return entries;
    }

    private static void Send(PlayerConnection connection, string text) => Assert.True(connection.Chat.Writer.TryWrite(text));

    private static Snapshot LastSnapshot(IEnumerable<ServerMessage> messages) =>
        messages.Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;

    // acc0 ("P0") bei (Far, Far), acc1 ("P1") bei (0, 0); nach einem Tick, Ausgänge geleert.
    private static (GameLoop Game, PlayerConnection Self, PlayerConnection Other) Joined()
    {
        var game = NewGame();
        var self = game.Join("acc0", "P0", (Far, Far));
        var other = game.Join("acc1", "P1", (0, 0));
        game.Tick();
        Drain(self);
        Drain(other);
        return (game, self, other);
    }

    [Fact] // AK-1, AK-5
    public void Successful_command_reaches_nobody_and_is_neither_in_history_nor_saved()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;
        game.SetVitals("acc0", hp: 10, mana: 0);
        game.SetVitals("acc1", hp: 10);
        game.Tick();
        Drain(self);
        Drain(other);

        Send(self, "/run fullHealth()");
        game.Tick();

        var own = Drain(self);
        var others = Drain(other);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(own));
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(others));
        Assert.Equal((float)GameSettings.MaxHp, LastSnapshot(own).Hp);
        Assert.Equal((float)GameSettings.MaxMana, LastSnapshot(own).Mana);
        Assert.Equal(10f, LastSnapshot(others).Hp);
        Assert.Empty(ReadQueue(game));
        var late = game.Join("acc2", "P2", null);
        game.Tick();
        Assert.Equal([ServerMessage.PayloadOneofCase.Welcome, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(late)));
    }

    [Theory] // AK-2
    [InlineData("hello")]
    [InlineData("run fullHealth()")]
    [InlineData("a /run fullHealth()")]
    public void Text_without_leading_slash_is_ordinary_chat(string text)
    {
        var (game, self, other) = Joined();
        using var gameScope = game;
        game.SetVitals("acc0", hp: 10);
        game.Tick();
        Drain(self);
        Drain(other);

        Send(self, text);
        game.Tick();

        var own = Drain(self);
        foreach (var messages in new[] { own, Drain(other) })
        {
            Assert.DoesNotContain(messages, m => m.PayloadCase == ServerMessage.PayloadOneofCase.CommandRejected);
            var chat = Assert.Single(messages, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage).ChatMessage;
            Assert.Equal(text, chat.Text);
            Assert.Equal("P0", chat.SenderName);
        }

        Assert.Equal(text, Assert.Single(ReadQueue(game)).Text);
        Assert.Equal(10f, LastSnapshot(own).Hp);
    }

    [Fact] // AK-1
    public void Command_is_recognized_after_normalizing()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;
        game.SetVitals("acc0", hp: 10);
        game.Tick();
        Drain(self);
        Drain(other);

        Send(self, "  /run fullHealth()\n");
        game.Tick();

        var own = Drain(self);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(own));
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(other)));
        Assert.Equal((float)GameSettings.MaxHp, LastSnapshot(own).Hp);
    }

    [Theory] // AK-4
    [InlineData("/xyz", CommandRejected.Types.Reason.UnknownCommand)]
    [InlineData("/run nope()", CommandRejected.Types.Reason.UnknownCommand)]
    [InlineData("/run", CommandRejected.Types.Reason.Invalid)]
    [InlineData("/run x(", CommandRejected.Types.Reason.Invalid)]
    [InlineData("/run fullHealth(1)", CommandRejected.Types.Reason.Invalid)]
    public void Unknown_or_invalid_command_is_rejected_for_sender_only_without_effect(string text, CommandRejected.Types.Reason reason)
    {
        var (game, self, other) = Joined();
        using var gameScope = game;
        game.SetVitals("acc0", hp: 10);
        game.Tick();
        Drain(self);
        Drain(other);

        Send(self, text);
        game.Tick();

        var own = Drain(self);
        Assert.Equal([ServerMessage.PayloadOneofCase.CommandRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(own));
        Assert.Equal(reason, own[0].CommandRejected.Reason);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(other)));
        var snapshot = LastSnapshot(own);
        Assert.Equal(10f, snapshot.Hp);
        var me = snapshot.Players.Single(p => p.Id == "acc0");
        Assert.Equal((float)Far, me.X);
        Assert.Equal((float)Far, me.Y);
        Assert.Equal(0u, me.SpawnSeq);
        Assert.Empty(ReadQueue(game));
    }

    [Fact] // AK-4
    public void Too_long_slash_text_is_dropped_silently()
    {
        var (game, self, _) = Joined();
        using var gameScope = game;

        Send(self, "/" + new string('x', GameSettings.ChatMaxLength));
        game.Tick();

        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(self)));
    }

    [Fact]
    public void Commands_count_toward_the_chat_rate_limit()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;

        for (var i = 0; i < GameSettings.ChatRateLimitCount + 1; i++)
        {
            Send(self, "/run resetPosition()");
        }

        Send(self, "hello");
        game.Tick();

        var own = Drain(self);
        Assert.Equal(2, own.Count(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatRejected
            && m.ChatRejected.Reason == ChatRejected.Types.Reason.TooFast));
        Assert.DoesNotContain(own, m => m.PayloadCase is ServerMessage.PayloadOneofCase.CommandRejected or ServerMessage.PayloadOneofCase.ChatMessage);
        Assert.DoesNotContain(Drain(other), m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage);
        Assert.Equal((uint)GameSettings.ChatRateLimitCount, LastSnapshot(own).Players.Single(p => p.Id == "acc0").SpawnSeq);
    }

    [Fact] // AK-8
    public void Reset_position_reaches_every_snapshot_with_new_spawn_seq()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;

        Send(self, "/run resetPosition()");
        game.Tick();

        foreach (var connection in new[] { self, other })
        {
            var players = LastSnapshot(Drain(connection)).Players;
            var moved = players.Single(p => p.Id == "acc0");
            Assert.Equal(1u, moved.SpawnSeq);
            Assert.True(Math.Sqrt((double)moved.X * moved.X + (double)moved.Y * moved.Y) <= World.SpawnRadius + 0.01);
            var stayed = players.Single(p => p.Id == "acc1");
            Assert.Equal((0f, 0f, 0u), (stayed.X, stayed.Y, stayed.SpawnSeq));
        }
    }
}
