using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>011 T6: Verlauf im GameLoop, ohne Datenbank.</summary>
public sealed class GameLoopChatHistoryTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
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

    private static List<ChatHistoryEntry> ReadQueue(GameLoop game)
    {
        var entries = new List<ChatHistoryEntry>();
        while (game.ChatToSave.TryRead(out var entry))
        {
            entries.Add(entry);
        }

        return entries;
    }

    private static void SendAcrossWindows(GameLoop game, FakeClock clock, PlayerConnection sender, int count)
    {
        for (var i = 0; i < count; i++)
        {
            clock.Advance(Window);
            Assert.True(sender.Chat.Writer.TryWrite($"m{i}"));
            game.Tick();
        }
    }

    [Fact] // AK-15
    public void Late_joiner_gets_history_right_after_welcome_before_any_snapshot()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        var sender = game.Join("a", "Anna", null);
        game.Tick();
        Assert.True(sender.Chat.Writer.TryWrite("hallo"));
        game.Tick();

        var late = game.Join("b", "Ben", null);
        game.Tick();

        var messages = Drain(late);
        Assert.Equal(
            [ServerMessage.PayloadOneofCase.Welcome, ServerMessage.PayloadOneofCase.ChatHistory, ServerMessage.PayloadOneofCase.Snapshot],
            messages.Select(m => m.PayloadCase));
        var chat = Assert.Single(messages[1].ChatHistory.Messages);
        Assert.Equal(("Anna", "hallo", Start.ToUnixTimeMilliseconds()), (chat.SenderName, chat.Text, chat.SentAtUnixMs));
    }

    [Fact] // AK-15
    public void History_holds_the_last_50_of_60_oldest_first()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        var sender = game.Join("a", "Anna", null);
        game.Tick();

        SendAcrossWindows(game, clock, sender, 60);
        Drain(sender);
        var late = game.Join("b", "Ben", null);
        game.Tick();

        var history = Drain(late).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory).ChatHistory;
        Assert.Equal(Enumerable.Range(10, 50).Select(i => $"m{i}"), history.Messages.Select(m => m.Text));
    }

    [Fact] // AK-16
    public void Message_of_exactly_24_hours_is_not_in_the_history_one_ms_younger_is()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        var sender = game.Join("a", "Anna", null);
        game.Tick();
        Assert.True(sender.Chat.Writer.TryWrite("hallo"));
        game.Tick();

        clock.Advance(TimeSpan.FromHours(GameSettings.ChatHistoryHours) - TimeSpan.FromMilliseconds(1));
        var young = game.Join("b", "Ben", null);
        game.Tick();
        Assert.Contains(Drain(young), m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory);

        clock.Advance(TimeSpan.FromMilliseconds(1));
        var old = game.Join("c", "Cem", null);
        game.Tick();
        Assert.DoesNotContain(Drain(old), m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory);
    }

    [Fact] // AK-15
    public void Rejected_and_empty_messages_stay_out_of_history_and_queue()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        var sender = game.Join("a", "Anna", null);
        game.Tick();

        Assert.True(sender.Chat.Writer.TryWrite("   "));
        for (var i = 0; i < GameSettings.ChatRateLimitCount + 1; i++)
        {
            Assert.True(sender.Chat.Writer.TryWrite($"m{i}"));
        }

        game.Tick();

        var expected = Enumerable.Range(0, GameSettings.ChatRateLimitCount).Select(i => $"m{i}").ToList();
        Assert.Equal(expected, ReadQueue(game).Select(e => e.Text));
        var late = game.Join("b", "Ben", null);
        game.Tick();
        var history = Drain(late).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory).ChatHistory;
        Assert.Equal(expected, history.Messages.Select(m => m.Text));
    }

    [Fact] // AK-15, AK-11
    public void Queue_gets_the_same_order_as_the_broadcast_with_account_name_and_time()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        var a = game.Join("a", "Anna", null);
        var b = game.Join("b", "Ben", null);
        game.Tick();
        Drain(a);

        Assert.True(a.Chat.Writer.TryWrite("a1"));
        Assert.True(b.Chat.Writer.TryWrite("b1"));
        Assert.True(a.Chat.Writer.TryWrite("a2"));
        game.Tick();

        var seen = Drain(a).Where(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage).Select(m => m.ChatMessage.Text).ToList();
        var queued = ReadQueue(game);
        Assert.Equal(seen, queued.Select(e => e.Text));
        Assert.Equal(new ChatHistoryEntry("a", "Anna", "a1", Start), queued.Single(e => e.Text == "a1"));
    }

    [Fact] // AK-15
    public void Loaded_history_comes_before_live_messages_and_reaches_a_join_in_the_first_tick()
    {
        var clock = new FakeClock(Start);
        using var game = NewGame(clock);
        game.LoadChatHistory([new("x", "Xaver", "alt", Start.AddMinutes(-1))]);
        var first = game.Join("a", "Anna", null);

        game.Tick();
        var history = Drain(first).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory).ChatHistory;
        Assert.Equal(["alt"], history.Messages.Select(m => m.Text));

        Assert.True(first.Chat.Writer.TryWrite("neu"));
        game.Tick();
        var second = game.Join("b", "Ben", null);
        game.Tick();
        var both = Drain(second).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory).ChatHistory;
        Assert.Equal(["alt", "neu"], both.Messages.Select(m => m.Text));
    }

    [Fact] // AK-15
    public void Save_queue_drops_oldest_counts_and_never_blocks()
    {
        var queue = new ChatSaveQueue(2);

        queue.Add(new("a", "A", "1", Start));
        queue.Add(new("a", "A", "2", Start));
        queue.Add(new("a", "A", "3", Start));

        Assert.Equal(1, queue.TakeDroppedCount());
        Assert.Equal(0, queue.TakeDroppedCount());
        Assert.True(queue.TryRead(out var first));
        Assert.Equal("2", first.Text);
        Assert.True(queue.TryRead(out var second));
        Assert.Equal("3", second.Text);
        Assert.False(queue.TryRead(out _));
    }
}
