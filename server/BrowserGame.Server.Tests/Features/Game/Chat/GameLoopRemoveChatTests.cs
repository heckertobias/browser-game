using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>011 T7: Nachrichten eines gelöschten Accounts im GameLoop, ohne Datenbank und ohne Netz.</summary>
public sealed class GameLoopRemoveChatTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

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

    private static void Send(PlayerConnection connection, string text) => Assert.True(connection.Chat.Writer.TryWrite(text));

    private static List<string> HistoryTexts(IEnumerable<ServerMessage> messages) =>
        [.. messages.Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory).ChatHistory.Messages.Select(m => m.Text)];

    private static bool HasHistory(IEnumerable<ServerMessage> messages) =>
        messages.Any(m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory);

    [Fact] // AK-17
    public void Join_in_the_same_tick_gets_history_without_the_account_and_others_go_on()
    {
        using var game = NewGame(new FakeClock(Start));
        var a = game.Join("a", "Anna", null);
        var b = game.Join("b", "Ben", null);
        game.Tick();
        Send(a, "a1");
        Send(b, "b1");
        Send(a, "a2");
        Send(b, "b2");
        game.Tick();

        game.ForceLeave("a", savePosition: false);
        game.RemoveChatOf("a");
        var c = game.Join("c", "Cara", null);
        game.Tick();

        var first = Drain(c);
        Assert.Equal(ServerMessage.PayloadOneofCase.Welcome, first[0].PayloadCase);
        Assert.Equal(["b1", "b2"], HistoryTexts(first));

        Send(b, "b3");
        game.Tick();
        var d = game.Join("d", "Dora", null);
        game.Tick();

        Assert.Equal(["b1", "b2", "b3"], HistoryTexts(Drain(d)));
    }

    [Fact] // AK-17
    public void History_with_only_that_account_sends_no_history()
    {
        using var game = NewGame(new FakeClock(Start));
        var a = game.Join("a", "Anna", null);
        game.Tick();
        Send(a, "a1");
        game.Tick();

        game.ForceLeave("a", savePosition: false);
        game.RemoveChatOf("a");
        var c = game.Join("c", "Cara", null);
        game.Tick();

        var messages = Drain(c);
        Assert.Contains(messages, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome);
        Assert.False(HasHistory(messages));
    }

    [Fact] // AK-17
    public void Join_queued_before_the_removal_still_gets_it_join_after_does_not()
    {
        using var game = NewGame(new FakeClock(Start));
        var a = game.Join("a", "Anna", null);
        game.Tick();
        Send(a, "a1");
        game.Tick();

        var early = game.Join("c", "Cara", null);
        game.ForceLeave("a", savePosition: false);
        game.RemoveChatOf("a");
        var late = game.Join("d", "Dora", null);
        game.Tick();

        Assert.Equal(["a1"], HistoryTexts(Drain(early)));
        Assert.False(HasHistory(Drain(late)));
    }

    [Fact] // AK-17
    public void Chat_waiting_at_force_leave_is_neither_broadcast_nor_saved_nor_in_history()
    {
        using var game = NewGame(new FakeClock(Start));
        var observer = game.Join("b", "Ben", null);
        var a = game.Join("a", "Anna", null);
        game.Tick();
        Drain(observer);

        Send(a, "zuletzt");
        game.ForceLeave("a", savePosition: false);
        game.RemoveChatOf("a");
        game.Tick();
        var late = game.Join("c", "Cara", null);
        game.Tick();

        Assert.DoesNotContain(Drain(observer), m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage);
        Assert.Empty(ReadQueue(game));
        Assert.False(HasHistory(Drain(late)));
    }

    [Fact] // AK-17
    public void Removal_in_a_later_tick_than_the_force_leave_works()
    {
        using var game = NewGame(new FakeClock(Start));
        var a = game.Join("a", "Anna", null);
        game.Tick();
        Send(a, "a1");
        game.Tick();

        game.ForceLeave("a", savePosition: false);
        game.Tick();
        Send(a, "spaet");
        game.RemoveChatOf("a");
        game.Tick();
        var late = game.Join("c", "Cara", null);
        game.Tick();

        Assert.False(HasHistory(Drain(late)));
    }

    [Fact] // AK-17
    public void Connection_joining_after_the_removal_cannot_write()
    {
        using var game = NewGame(new FakeClock(Start));
        var observer = game.Join("b", "Ben", null);
        var first = game.Join("a", "Anna", null);
        game.Tick();

        game.ForceLeave("a", savePosition: false);
        game.RemoveChatOf("a");
        var second = game.Join("a", "Anna", null);
        game.Tick();
        Drain(observer);
        Drain(first);
        Drain(second);
        ReadQueue(game);

        Send(second, "danach");
        game.Tick();
        var late = game.Join("c", "Cara", null);
        game.Tick();

        Assert.DoesNotContain(Drain(observer), m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage);
        Assert.DoesNotContain(Drain(second), m =>
            m.PayloadCase is ServerMessage.PayloadOneofCase.ChatMessage or ServerMessage.PayloadOneofCase.ChatRejected);
        Assert.Empty(ReadQueue(game));
        Assert.False(HasHistory(Drain(late)));
    }

    [Fact] // AK-17
    public void Loaded_messages_of_the_account_are_removed_too()
    {
        using var game = NewGame(new FakeClock(Start));
        game.LoadChatHistory([new("x", "Xaver", "weg", Start.AddMinutes(-2)), new("y", "Yara", "bleibt", Start.AddMinutes(-1))]);

        game.RemoveChatOf("x");
        var c = game.Join("c", "Cara", null);
        game.Tick();

        Assert.Equal(["bleibt"], HistoryTexts(Drain(c)));
    }

    [Fact] // AK-17
    public void Removing_an_unknown_account_changes_nothing()
    {
        using var game = NewGame(new FakeClock(Start));
        var a = game.Join("a", "Anna", null);
        game.Tick();
        Send(a, "a1");
        game.Tick();

        game.RemoveChatOf("fehlt");
        game.Tick();
        Send(a, "a2");
        game.Tick();
        var late = game.Join("c", "Cara", null);
        game.Tick();

        Assert.Equal(["a1", "a2"], HistoryTexts(Drain(late)));
        Assert.False(a.WasKicked);
    }
}
