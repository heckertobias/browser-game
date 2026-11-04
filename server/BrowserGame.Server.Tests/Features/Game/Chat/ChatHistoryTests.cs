using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class ChatHistoryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now.AddHours(-24);

    private static ChatHistory NewHistory() =>
        new(GameSettings.ChatHistoryCount, TimeSpan.FromHours(GameSettings.ChatHistoryHours));

    private static ChatHistoryEntry Message(string text, string account = "a", DateTimeOffset? sentAt = null) =>
        new(account, $"name-{account}", text, sentAt ?? Now);

    private static string Numbered(int number) => $"m{number}";

    private static IEnumerable<string> NumberedRange(int first, int last) =>
        Enumerable.Range(first, last - first + 1).Select(Numbered);

    private static void AddRange(ChatHistory history, int first, int last, Func<int, string>? account = null)
    {
        for (var i = first; i <= last; i++)
        {
            history.Add(Message(Numbered(i), account?.Invoke(i) ?? "a"));
        }
    }

    private static IEnumerable<string> Texts(ChatHistory history) => history.Recent(Now).Select(m => m.Text);

    private static string EvenA(int i) => i % 2 == 0 ? "a" : "b";

    [Fact]
    public void Empty_history_returns_nothing()
    {
        Assert.Empty(NewHistory().Recent(Now));
    }

    [Fact]
    public void Entry_keeps_all_its_fields()
    {
        var history = NewHistory();
        var entry = Message("x", "a", Now.AddHours(-1));
        history.Add(entry);

        Assert.Equal(entry, Assert.Single(history.Recent(Now)));
    }

    [Fact]
    public void Messages_come_oldest_first()
    {
        var history = NewHistory();
        AddRange(history, 1, 3);

        Assert.Equal(NumberedRange(1, 3), Texts(history));
    }

    [Fact]
    public void Exactly_50_messages_are_all_kept()
    {
        var history = NewHistory();
        AddRange(history, 1, 50);

        Assert.Equal(NumberedRange(1, 50), Texts(history));
    }

    [Fact]
    public void The_51st_message_displaces_the_oldest()
    {
        var history = NewHistory();
        AddRange(history, 1, 51);

        Assert.Equal(NumberedRange(2, 51), Texts(history));
    }

    [Fact]
    public void After_many_messages_the_newest_50_remain()
    {
        var history = NewHistory();
        AddRange(history, 1, 175);

        Assert.Equal(NumberedRange(126, 175), Texts(history));
    }

    [Fact]
    public void Arrival_order_wins_over_the_timestamps()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Now));
        history.Add(Message("y", sentAt: Now.AddMinutes(-1)));

        Assert.Equal(["x", "y"], Texts(history));
    }

    [Fact]
    public void Limits_come_from_the_parameters()
    {
        var small = new ChatHistory(2, TimeSpan.FromHours(1));
        for (var i = 1; i <= 3; i++)
        {
            small.Add(Message(Numbered(i)));
        }

        var shortLived = new ChatHistory(2, TimeSpan.FromHours(1));
        shortLived.Add(Message("old", sentAt: Now.AddHours(-1)));

        Assert.Equal(["m2", "m3"], small.Recent(Now).Select(m => m.Text));
        Assert.Empty(shortLived.Recent(Now));
    }

    [Fact]
    public void Message_just_under_24_hours_old_is_returned()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Cutoff.AddTicks(1)));

        Assert.Single(history.Recent(Now));
    }

    [Fact]
    public void Message_of_exactly_24_hours_is_not_returned()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Cutoff));

        Assert.Empty(history.Recent(Now));
    }

    [Fact]
    public void Message_just_over_24_hours_old_is_not_returned()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Cutoff.AddTicks(-1)));

        Assert.Empty(history.Recent(Now));
    }

    [Fact]
    public void Message_expires_as_time_passes()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Now));

        Assert.Single(history.Recent(Now.AddHours(24).AddTicks(-1)));
        Assert.Empty(history.Recent(Now.AddHours(24)));
    }

    [Fact]
    public void Recent_does_not_change_the_history()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Now));

        Assert.Empty(history.Recent(Now.AddHours(25)));
        Assert.Single(history.Recent(Now));
    }

    [Fact]
    public void Expired_messages_are_filtered_wherever_they_are()
    {
        var history = NewHistory();
        history.Add(Message("alt1", sentAt: Now.AddHours(-25)));
        history.Add(Message("neu1", sentAt: Now.AddHours(-23)));
        history.Add(Message("alt2", sentAt: Now.AddHours(-25)));
        history.Add(Message("neu2", sentAt: Now.AddHours(-1)));

        Assert.Equal(["neu1", "neu2"], Texts(history));
    }

    [Fact]
    public void Full_history_with_expired_messages_returns_only_the_valid_ones()
    {
        var history = NewHistory();
        for (var i = 1; i <= 30; i++)
        {
            history.Add(Message(Numbered(i), sentAt: Now.AddHours(-25)));
        }

        for (var i = 31; i <= 50; i++)
        {
            history.Add(Message(Numbered(i)));
        }

        Assert.Equal(NumberedRange(31, 50), Texts(history));
    }

    [Fact]
    public void Message_with_a_time_after_now_is_returned()
    {
        var history = NewHistory();
        history.Add(Message("x", sentAt: Now.AddMinutes(1)));

        Assert.Single(history.Recent(Now));
    }

    [Fact]
    public void RemoveAccount_removes_only_that_account_and_keeps_the_order()
    {
        var history = NewHistory();
        AddRange(history, 1, 6, i => i % 2 == 0 ? "a" : "b");

        history.RemoveAccount("b");

        Assert.Equal(["m2", "m4", "m6"], Texts(history));
    }

    [Fact]
    public void RemoveAccount_of_an_unknown_account_changes_nothing()
    {
        var history = NewHistory();
        AddRange(history, 1, 50);

        history.RemoveAccount("x");
        Assert.Equal(NumberedRange(1, 50), Texts(history));

        AddRange(history, 51, 51);
        Assert.Equal(NumberedRange(2, 51), Texts(history));
    }

    [Fact]
    public void RemoveAccount_of_the_only_account_empties_the_history()
    {
        var history = NewHistory();
        AddRange(history, 1, 50);

        history.RemoveAccount("a");
        Assert.Empty(history.Recent(Now));

        AddRange(history, 101, 150);
        Assert.Equal(NumberedRange(101, 150), Texts(history));

        AddRange(history, 151, 151);
        Assert.Equal(NumberedRange(102, 151), Texts(history));
    }

    [Fact]
    public void RemoveAccount_in_a_wrapped_history_keeps_the_order()
    {
        var history = NewHistory();
        AddRange(history, 1, 75, EvenA);

        history.RemoveAccount("b");

        var expected = Enumerable.Range(26, 49).Where(i => i % 2 == 0).Select(Numbered).ToArray();
        Assert.Equal(25, expected.Length);
        Assert.Equal(expected, Texts(history));
    }

    [Fact]
    public void After_RemoveAccount_new_messages_fill_up_to_50_then_displace_the_oldest()
    {
        var history = NewHistory();
        AddRange(history, 1, 75, EvenA);
        history.RemoveAccount("b");

        AddRange(history, 101, 125);
        var evens = Enumerable.Range(26, 49).Where(i => i % 2 == 0).Select(Numbered);
        var afterFill = evens.Concat(NumberedRange(101, 125)).ToArray();
        Assert.Equal(50, afterFill.Length);
        Assert.Equal(afterFill, Texts(history));

        AddRange(history, 126, 126);
        var evensAfter = Enumerable.Range(28, 47).Where(i => i % 2 == 0).Select(Numbered);
        var afterDisplace = evensAfter.Concat(NumberedRange(101, 126)).ToArray();
        Assert.Equal(50, afterDisplace.Length);
        Assert.Equal(afterDisplace, Texts(history));
    }

    [Fact]
    public void RemoveAccount_does_not_bring_back_displaced_messages()
    {
        var history = NewHistory();
        AddRange(history, 1, 60, i => i % 5 == 0 ? "a" : "b");

        history.RemoveAccount("a");

        var expected = Enumerable.Range(11, 50).Where(i => i % 5 != 0).Select(Numbered).ToArray();
        Assert.Equal(40, expected.Length);
        Assert.Equal(expected, Texts(history));
    }

    [Fact]
    public void Result_of_Recent_is_a_snapshot()
    {
        var history = NewHistory();
        AddRange(history, 1, 50, EvenA);
        var result = history.Recent(Now);

        AddRange(history, 51, 51);
        history.RemoveAccount("b");

        Assert.Equal(NumberedRange(1, 50), result.Select(m => m.Text));
    }

    [Fact]
    public void Each_call_of_Recent_returns_its_own_result()
    {
        var history = NewHistory();
        history.Add(Message("x"));

        Assert.NotSame(history.Recent(Now), history.Recent(Now));
    }
}
