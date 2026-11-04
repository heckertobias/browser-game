using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public sealed class GameLoopChatTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan TickLength = TimeSpan.FromSeconds(1.0 / GameSettings.TickRateHz);
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(GameSettings.ChatRateLimitSeconds);

    private static GameLoop NewGame(FakeClock clock) => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build(), clock);

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static List<ChatMessage> Chats(IEnumerable<ServerMessage> messages) =>
        [.. messages.Where(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage).Select(m => m.ChatMessage)];

    private static List<ChatRejected> Rejections(IEnumerable<ServerMessage> messages) =>
        [.. messages.Where(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatRejected).Select(m => m.ChatRejected)];

    private static List<ServerMessage.PayloadOneofCase> Payloads(IEnumerable<ServerMessage> messages) => [.. messages.Select(m => m.PayloadCase)];

    private static List<string> Texts(IEnumerable<ServerMessage> messages) => [.. Chats(messages).Select(c => c.Text)];

    private static void Send(PlayerConnection connection, string text) => Assert.True(connection.Chat.Writer.TryWrite(text));

    private static (GameLoop Game, FakeClock Clock) Started()
    {
        var clock = new FakeClock(Start);
        return (NewGame(clock), clock);
    }

    private static PlayerConnection[] JoinPlayers(GameLoop game, int count)
    {
        var players = Enumerable.Range(0, count).Select(i => game.Join($"acc{i}", $"P{i}", (0, 0))).ToArray();
        game.Tick();
        foreach (var player in players)
        {
            Drain(player);
        }

        return players;
    }

    [Fact] // AK-10
    public void Accepted_message_reaches_all_connections_including_sender()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 3);

        Send(players[1], "hello");
        game.Tick();

        foreach (var player in players)
        {
            var messages = Drain(player);
            var chat = Assert.Single(Chats(messages));
            Assert.Equal("P1", chat.SenderName);
            Assert.Equal("hello", chat.Text);
            Assert.Equal(clock.GetUtcNow().ToUnixTimeMilliseconds(), chat.SentAtUnixMs);
            Assert.Empty(Rejections(messages));
        }
    }

    [Fact] // AK-10
    public void Text_is_normalized_before_it_is_distributed()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        Send(players[0], "  a\nb  ");
        game.Tick();

        Assert.Equal("a b", Assert.Single(Chats(Drain(players[1]))).Text);
    }

    [Fact] // AK-13
    public void Empty_or_too_long_text_reaches_nobody_and_does_not_count()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        for (var i = 0; i < 3; i++)
        {
            Send(players[0], "   ");
            Send(players[0], new string('x', GameSettings.ChatMaxLength + 1));
            game.Tick();
        }

        Assert.Empty(Chats(Drain(players[1])));
        Assert.Empty(Rejections(Drain(players[0])));

        for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
        {
            Send(players[0], $"ok{i}");
        }

        game.Tick();
        var messages = Drain(players[0]);
        Assert.Equal(GameSettings.ChatRateLimitCount, Chats(messages).Count);
        Assert.Empty(Rejections(messages));
    }

    [Fact] // AK-13
    public void Sixth_message_in_window_is_rejected_for_sender_only()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        for (var i = 0; i < GameSettings.ChatRateLimitCount + 1; i++)
        {
            Send(players[0], $"m{i}");
        }

        game.Tick();

        var sender = Drain(players[0]);
        Assert.Equal(GameSettings.ChatRateLimitCount, Chats(sender).Count);
        var rejected = Assert.Single(Rejections(sender));
        Assert.Equal(ChatRejected.Types.Reason.TooFast, rejected.Reason);
        Assert.Equal(ServerMessage.PayloadOneofCase.ChatRejected, sender.Where(m => m.PayloadCase != ServerMessage.PayloadOneofCase.Snapshot).Last().PayloadCase);

        var other = Drain(players[1]);
        Assert.Equal(GameSettings.ChatRateLimitCount, Chats(other).Count);
        Assert.Empty(Rejections(other));
    }

    [Fact] // AK-13
    public void Rejected_text_is_not_distributed()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
        {
            Send(players[0], $"ok{i}");
        }

        Send(players[0], "rejected-text");
        game.Tick();

        Assert.DoesNotContain("rejected-text", Texts(Drain(players[0])));
        Assert.DoesNotContain("rejected-text", Texts(Drain(players[1])));
    }

    [Fact] // AK-13
    public void Limit_opens_again_after_window()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 1);
        for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
        {
            Send(players[0], $"m{i}");
        }

        game.Tick();
        Drain(players[0]);

        clock.Advance(Window - TimeSpan.FromMilliseconds(1));
        Send(players[0], "early");
        game.Tick();
        var early = Drain(players[0]);
        Assert.Empty(Chats(early));
        Assert.Single(Rejections(early));

        clock.Advance(TimeSpan.FromMilliseconds(1));
        Send(players[0], "later");
        game.Tick();
        var later = Drain(players[0]);
        Assert.Single(Chats(later));
        Assert.Empty(Rejections(later));
    }

    [Fact] // AK-13
    public void Limit_holds_across_reconnect()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var first = JoinPlayers(game, 1)[0];
        for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
        {
            Send(first, $"m{i}");
        }

        game.Tick();
        game.Leave(first);
        var second = game.Join("acc0", "P0", null);
        Send(second, "again");
        game.Tick();

        var messages = Drain(second);
        Assert.Empty(Chats(messages));
        Assert.Single(Rejections(messages));
    }

    [Fact] // AK-13
    public void Flood_over_channel_capacity_drops_oldest_and_rejects_rest()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        var total = GameSettings.ChatInboxCapacity + 1;
        for (var i = 1; i <= total; i++)
        {
            Send(players[0], $"t{i}");
        }

        game.Tick();

        var messages = Drain(players[1]);
        Assert.Equal(["t2", "t3", "t4", "t5", "t6"], Texts(messages));
        var sender = Drain(players[0]);
        Assert.Equal(total - 1 - GameSettings.ChatRateLimitCount, Rejections(sender).Count);
    }

    [Fact] // AK-11
    public void All_connections_see_messages_in_the_same_order()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 3);

        for (var round = 0; round < 4; round++)
        {
            clock.Advance(Window);
            for (var p = 0; p < players.Length; p++)
            {
                Send(players[p], $"{p}-{round}a");
                if (round % 2 == 0)
                {
                    Send(players[p], $"{p}-{round}b");
                }
            }

            game.Tick();
        }

        var seen = players.Select(p => Texts(Drain(p))).ToArray();
        Assert.Equal(18, seen[0].Count);
        Assert.Equal(seen[0], seen[1]);
        Assert.Equal(seen[0], seen[2]);
        for (var p = 0; p < players.Length; p++)
        {
            var own = seen[0].Where(t => t.StartsWith($"{p}-", StringComparison.Ordinal)).ToList();
            Assert.Equal(own.Order().ToList(), own);
        }
    }

    [Fact] // AK-11
    public void Welcome_precedes_chat_and_chat_precedes_snapshot_of_its_tick()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var connection = game.Join("acc", "A", null);
        Send(connection, "hi");

        game.Tick();

        Assert.Equal(
            [ServerMessage.PayloadOneofCase.Welcome, ServerMessage.PayloadOneofCase.ChatMessage, ServerMessage.PayloadOneofCase.Snapshot],
            Payloads(Drain(connection)));
    }

    [Fact]
    public void Without_chat_the_message_sequence_is_unchanged()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var connection = game.Join("acc", "A", null);

        game.Tick();
        Assert.Equal([ServerMessage.PayloadOneofCase.Welcome, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(connection)));

        game.Tick();
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(connection)));
    }

    [Fact]
    public void Chat_of_connection_that_left_in_the_same_tick_is_dropped()
    {
        foreach (var force in new[] { false, true })
        {
            var (game, _) = Started();
            using var gameScope = game;
            var players = JoinPlayers(game, 2);

            Send(players[0], "lost");
            if (force)
            {
                game.ForceLeave("acc0", true);
            }
            else
            {
                game.Leave(players[0]);
            }

            game.Tick();

            Assert.Empty(Chats(Drain(players[1])));
        }
    }

    [Fact] // AK-10, AK-11
    public void Hundred_players_bursting_at_the_limit_lose_no_chat_message()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 100);

        for (var p = 0; p < players.Length; p++)
        {
            for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
            {
                Send(players[p], $"{p}-{i}");
            }
        }

        game.Tick();
        for (var i = 0; i < 19; i++)
        {
            game.Tick();
        }

        var reference = Texts(Drain(players[0]));
        Assert.Equal(100 * GameSettings.ChatRateLimitCount, reference.Count);
        foreach (var player in players.Skip(1))
        {
            var messages = Drain(player);
            Assert.Equal(reference, Texts(messages));
            Assert.Empty(Rejections(messages));
            var snapshot = Assert.Single(messages, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot);
            Assert.Equal(21u, snapshot.Snapshot.Tick); // 1 Tick beim Beitritt, dann 20
        }
    }

    [Fact] // AK-10, AK-11
    public void Hundred_players_at_the_limit_with_stalled_clients_lose_no_chat_message()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 100);
        var sent = 0;

        for (var tick = 0; tick < 400; tick++)
        {
            clock.Advance(TickLength);
            for (var target = (tick + 1) * 5 / 2; sent < target; sent++)
            {
                Send(players[sent % 100], $"m{sent}");
            }

            game.Tick();
        }

        Assert.Equal(1000, sent);
        var reference = Texts(Drain(players[0]));
        Assert.Equal(1000, reference.Count);
        foreach (var player in players.Skip(1))
        {
            var messages = Drain(player);
            Assert.Equal(reference, Texts(messages));
            Assert.Empty(Rejections(messages));
        }
    }

    [Fact] // AK-10
    public void Lagging_client_keeps_all_chat_and_only_newest_snapshot()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        const int ticks = 50;
        for (var tick = 1; tick <= ticks; tick++)
        {
            if (tick % 15 == 0)
            {
                clock.Advance(Window);
                Send(players[1], $"m{tick}");
            }

            game.Tick();
        }

        var messages = Drain(players[0]);
        Assert.Equal(["m15", "m30", "m45"], Texts(messages));
        var snapshot = Assert.Single(messages, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot);
        Assert.Equal((uint)ticks + 1, snapshot.Snapshot.Tick);
    }

    [Fact]
    public void Waiting_snapshot_is_not_sent_after_kick()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var connection = game.Join("acc", "A", null);
        game.Tick();

        game.ForceLeave("acc", true);
        game.Tick();

        Assert.Equal([ServerMessage.PayloadOneofCase.Welcome, ServerMessage.PayloadOneofCase.Kicked], Payloads(Drain(connection)));
    }

    [Fact]
    public void Stalled_client_is_cut_off_without_affecting_others()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var aborted = 0;
        var observer = game.Join("obs", "Obs", null, () => aborted++);
        var sender = game.Join("snd", "Snd", null);
        game.Tick();
        Drain(sender);

        var ticks = GameSettings.OutboxRequiredLimit / GameSettings.ChatRateLimitCount + 5;
        var received = new List<ServerMessage>();
        for (var tick = 0; tick < ticks; tick++)
        {
            clock.Advance(Window);
            for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
            {
                Send(sender, $"m{tick}-{i}");
            }

            game.Tick();
            received.AddRange(Drain(sender));
        }

        Assert.Equal(1, aborted);
        Assert.False(observer.Outbox.Reader.TryRead(out _));
        Assert.True(observer.Outbox.Reader.Completion.IsCompleted);
        Assert.False(observer.WasKicked);
        Assert.Equal(ticks * GameSettings.ChatRateLimitCount, Chats(received).Count);
        Assert.Empty(Rejections(received));
        Assert.DoesNotContain(received, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Kicked);

        game.Leave(observer);
        game.Tick();
        var last = Drain(sender).Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;
        Assert.True(last.Players.Single(p => p.Id == "obs").Away);
    }

    [Fact] // AK-10
    public void Message_is_serialized_once_and_shared_between_connections()
    {
        var (game, _) = Started();
        using var gameScope = game;
        var players = JoinPlayers(game, 2);

        Send(players[0], "shared");
        game.Tick();

        Assert.True(players[0].Outbox.Reader.TryRead(out var first));
        Assert.True(players[1].Outbox.Reader.TryRead(out var second));
        Assert.Equal(ServerMessage.PayloadOneofCase.ChatMessage, ServerMessage.Parser.ParseFrom(first).PayloadCase);
        Assert.Same(first, second);
    }

    [Fact]
    public void Kicked_still_arrives_when_required_messages_are_at_the_limit()
    {
        var (game, clock) = Started();
        using var gameScope = game;
        var aborted = 0;
        var observer = game.Join("obs", "Obs", null, () => aborted++);
        var sender = game.Join("snd", "Snd", null);
        game.Tick();

        // Welcome plus Chats füllen die Pflichtnachrichten genau bis zur Grenze.
        var chats = GameSettings.OutboxRequiredLimit - 1;
        for (var left = chats; left > 0; left -= GameSettings.ChatRateLimitCount)
        {
            clock.Advance(Window);
            for (var i = 0; i < Math.Min(left, GameSettings.ChatRateLimitCount); i++)
            {
                Send(sender, $"m{left}-{i}");
            }

            game.Tick();
            Drain(sender);
        }

        Assert.Equal(0, aborted);
        game.ForceLeave("obs", true);
        game.Tick();

        var messages = Drain(observer);
        Assert.Equal(0, aborted);
        Assert.Equal(GameSettings.OutboxRequiredLimit + 1, messages.Count);
        Assert.Equal(ServerMessage.PayloadOneofCase.Kicked, messages[^1].PayloadCase);
        Assert.True(observer.WasKicked);
    }

    [Fact]
    public void Container_supplies_registered_clock_to_game_loop()
    {
        var clock = new FakeClock(Start);
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(clock);
        services.AddSingleton<ILogger<GameLoop>>(NullLogger<GameLoop>.Instance);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.AddGame();
        using var provider = services.BuildServiceProvider();
        var game = provider.GetRequiredService<GameLoop>();
        var connection = game.Join("acc", "A", null);

        Send(connection, "hi");
        game.Tick();

        Assert.Equal(Start.ToUnixTimeMilliseconds(), Assert.Single(Chats(Drain(connection))).SentAtUnixMs);
    }
}
