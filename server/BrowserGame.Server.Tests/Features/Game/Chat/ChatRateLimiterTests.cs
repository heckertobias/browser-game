using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class ChatRateLimiterTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);

    private static ChatRateLimiter NewLimiter() =>
        new(GameSettings.ChatRateLimitCount, TimeSpan.FromSeconds(GameSettings.ChatRateLimitSeconds));

    private static bool Send(ChatRateLimiter limiter, string account, double seconds) =>
        limiter.TryAccept(account, Start.AddSeconds(seconds));

    private static void AssertAcceptedFiveAt(ChatRateLimiter limiter, string account, double seconds)
    {
        for (var i = 0; i < 5; i++)
        {
            Assert.True(Send(limiter, account, seconds));
        }
    }

    [Fact]
    public void Five_messages_at_once_are_accepted_the_sixth_is_rejected()
    {
        var limiter = NewLimiter();

        AssertAcceptedFiveAt(limiter, "a", 0);

        Assert.False(Send(limiter, "a", 0));
    }

    [Fact]
    public void Five_messages_spread_over_the_window_are_accepted_the_sixth_is_rejected()
    {
        var limiter = NewLimiter();

        foreach (var seconds in new double[] { 0, 2, 4, 6, 8 })
        {
            Assert.True(Send(limiter, "a", seconds));
        }

        Assert.False(Send(limiter, "a", 9));
    }

    [Fact]
    public void Message_still_counts_just_before_10_seconds()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        Assert.False(limiter.TryAccept("a", Start.AddSeconds(10).AddTicks(-1)));
    }

    [Fact]
    public void Message_no_longer_counts_at_exactly_10_seconds()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        Assert.True(limiter.TryAccept("a", Start.AddSeconds(10)));
    }

    [Fact]
    public void Rejected_messages_do_not_count()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        foreach (var seconds in new[] { 1, 5, 9.5 })
        {
            Assert.False(Send(limiter, "a", seconds));
        }

        AssertAcceptedFiveAt(limiter, "a", 10);
        Assert.False(Send(limiter, "a", 10));
    }

    [Fact]
    public void Exactly_five_are_accepted_again_after_the_window()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        AssertAcceptedFiveAt(limiter, "a", 10);

        Assert.False(Send(limiter, "a", 10));
    }

    [Fact]
    public void Window_slides_with_each_message()
    {
        var limiter = NewLimiter();

        foreach (var seconds in new double[] { 0, 1, 2, 3, 4 })
        {
            Assert.True(Send(limiter, "a", seconds));
        }

        Assert.False(Send(limiter, "a", 9));
        Assert.True(Send(limiter, "a", 10));
        Assert.False(Send(limiter, "a", 10.5));
        Assert.True(Send(limiter, "a", 11));
    }

    [Fact]
    public void Limit_applies_per_account()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        Assert.False(Send(limiter, "a", 0));
        Assert.True(Send(limiter, "b", 0));
        Assert.False(Send(limiter, "a", 0));
    }

    [Fact]
    public void Own_messages_expire_between_cleanups()
    {
        var limiter = NewLimiter();
        Assert.True(Send(limiter, "b", 0));
        AssertAcceptedFiveAt(limiter, "a", 1);
        Assert.True(Send(limiter, "b", 10));

        Assert.False(limiter.TryAccept("a", Start.AddSeconds(11).AddTicks(-1)));
        AssertAcceptedFiveAt(limiter, "a", 11);
        Assert.False(Send(limiter, "a", 11));
    }

    [Fact]
    public void Inactive_account_stays_until_the_window_has_passed()
    {
        var limiter = NewLimiter();
        Assert.True(Send(limiter, "a", 0));

        Assert.True(limiter.TryAccept("b", Start.AddSeconds(10).AddTicks(-1)));
        Assert.Equal(2, limiter.TrackedAccounts);

        Assert.True(Send(limiter, "b", 10));
        Assert.Equal(1, limiter.TrackedAccounts);
    }

    [Fact]
    public void Inactive_accounts_are_removed_at_most_once_per_window()
    {
        var limiter = NewLimiter();
        Assert.True(Send(limiter, "b", 0));
        Assert.True(Send(limiter, "a", 1));

        Assert.True(Send(limiter, "c", 10));
        Assert.Equal(2, limiter.TrackedAccounts);

        Assert.True(Send(limiter, "c", 11));
        Assert.Equal(2, limiter.TrackedAccounts);

        Assert.True(Send(limiter, "c", 19));
        Assert.Equal(2, limiter.TrackedAccounts);

        Assert.True(Send(limiter, "c", 20));
        Assert.Equal(1, limiter.TrackedAccounts);
    }

    [Fact]
    public void Accounts_without_counting_messages_are_forgotten()
    {
        var limiter = NewLimiter();
        for (var i = 0; i < 1000; i++)
        {
            Assert.True(Send(limiter, $"account-{i}", 0));
        }

        Assert.Equal(1000, limiter.TrackedAccounts);

        Assert.True(Send(limiter, "other", 10));

        Assert.Equal(1, limiter.TrackedAccounts);
    }

    [Fact]
    public void Accounts_in_the_window_are_kept()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);

        Assert.True(Send(limiter, "b", 9));

        Assert.Equal(2, limiter.TrackedAccounts);
        Assert.False(Send(limiter, "a", 9));
    }

    [Fact]
    public void Forgotten_account_starts_with_a_full_allowance()
    {
        var limiter = NewLimiter();
        AssertAcceptedFiveAt(limiter, "a", 0);
        Assert.True(Send(limiter, "b", 10));

        AssertAcceptedFiveAt(limiter, "a", 10);

        Assert.False(Send(limiter, "a", 10));
    }
}
