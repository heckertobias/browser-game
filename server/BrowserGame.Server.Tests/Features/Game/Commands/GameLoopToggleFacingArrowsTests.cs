using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>017 T3: toggleFacingArrows im GameLoop, ohne Netz und DB.</summary>
public sealed class GameLoopToggleFacingArrowsTests
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

    [Fact] // AK-11, AK-12
    public void Toggle_reaches_only_the_sender_and_is_neither_chat_nor_saved()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;

        Send(self, "/run toggleFacingArrows()");
        game.Tick();

        Assert.Equal([ServerMessage.PayloadOneofCase.ToggleFacingArrows, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(self)));
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(other)));
        Assert.Empty(ReadQueue(game));
    }

    [Fact] // AK-11
    public void Toggle_twice_sends_two_messages_to_the_sender()
    {
        var (game, self, other) = Joined();
        using var gameScope = game;

        Send(self, "/run toggleFacingArrows()");
        Send(self, "/run toggleFacingArrows()");
        game.Tick();

        Assert.Equal(
            [ServerMessage.PayloadOneofCase.ToggleFacingArrows, ServerMessage.PayloadOneofCase.ToggleFacingArrows, ServerMessage.PayloadOneofCase.Snapshot],
            Payloads(Drain(self)));
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(other)));
    }
}
