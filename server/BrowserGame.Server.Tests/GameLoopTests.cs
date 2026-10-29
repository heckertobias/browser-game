using BrowserGame.Contracts.V1;
using BrowserGame.Server.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public class GameLoopTests
{
    private const uint LingerTicks = 20; // Game:LingerSeconds = 1 bei 20 Hz

    private static GameLoop NewGame() => new(
        NullLogger<GameLoop>.Instance,
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GameSettings.LingerSecondsKey] = "1" })
            .Build());

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static Snapshot LastSnapshot(PlayerConnection observer) =>
        Drain(observer).Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;

    /// <summary>Figur im letzten Snapshot, den <paramref name="observer"/> bekommen hat.</summary>
    private static PlayerState? Seen(PlayerConnection observer, string id) =>
        LastSnapshot(observer).Players.SingleOrDefault(p => p.Id == id);

    private static void TickTimes(GameLoop game, uint count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.Empty(game.Tick());
        }
    }

    [Fact] // AK-12
    public void Second_join_kicks_first_with_signed_in_elsewhere()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();
        var b = game.Join("acc", "A", null);
        game.Tick();

        var last = Drain(a).Last();
        Assert.Equal(ServerMessage.PayloadOneofCase.Kicked, last.PayloadCase);
        Assert.Equal(Kicked.Types.Reason.SignedInElsewhere, last.Kicked.Reason);
        Assert.True(a.WasKicked);
        Assert.True(a.Outbox.Reader.Completion.IsCompleted);
        Assert.Equal("acc", Drain(b).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome.PlayerId);
        Assert.Single(LastSnapshot(observer).Players, p => p.Id == "acc");
    }

    [Fact] // AK-12
    public void Takeover_keeps_existing_figure_and_position()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        game.Join("acc", "A", (100, 50));
        game.Tick();
        game.Join("acc", "A", (0, 0));
        game.Tick();

        var figure = Seen(observer, "acc")!;
        Assert.Equal((100f, 50f), (figure.X, figure.Y));
        Assert.False(figure.Away);
    }

    [Fact] // AK-12
    public void New_connection_controls_figure_after_takeover()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (100, 50));
        game.Tick();
        var b = game.Join("acc", "A", null);
        game.Tick();

        b.Inbox.Writer.TryWrite(new MoveInput(1, 1, 0));
        game.Tick();
        var moved = Seen(observer, "acc")!.X;
        a.Inbox.Writer.TryWrite(new MoveInput(1000, 0, 1));
        game.Tick();
        var after = Seen(observer, "acc")!;

        Assert.True(moved > 100);
        Assert.Equal(50f, after.Y);
    }

    [Fact] // AK-12
    public void Late_leave_of_kicked_connection_is_ignored()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();
        var b = game.Join("acc", "A", null);
        game.Tick();

        game.Leave(a);
        TickTimes(game, LingerTicks + 2);

        Assert.False(Seen(observer, "acc")!.Away);
        Assert.False(b.WasKicked);
    }

    [Fact] // AK-20
    public void Leave_keeps_figure_away_and_visible()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (5, 6));
        game.Tick();
        game.Leave(a);
        game.Tick();

        var figure = Seen(observer, "acc")!;
        Assert.True(figure.Away);
        Assert.Equal((5f, 6f), (figure.X, figure.Y));
        Assert.DoesNotContain(Drain(a), m => m.PayloadCase == ServerMessage.PayloadOneofCase.Kicked);
    }

    [Fact] // AK-20, AK-23
    public void Away_figure_is_removed_after_linger_and_returned()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (7, 8));
        game.Tick();
        game.Leave(a);

        TickTimes(game, LingerTicks);
        Assert.NotNull(Seen(observer, "acc"));
        var removed = Assert.Single(game.Tick());

        Assert.Equal("acc", removed.Id);
        Assert.Equal((7d, 8d), (removed.X, removed.Y));
        Assert.Null(Seen(observer, "acc"));
    }

    [Fact] // AK-21
    public void Rejoin_within_linger_takes_over_waiting_figure()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (10, 20));
        game.Tick();
        game.Leave(a);
        TickTimes(game, 5);
        var b = game.Join("acc", "A", null);
        game.Tick();

        var figure = Seen(observer, "acc")!;
        Assert.Equal((10f, 20f), (figure.X, figure.Y));
        Assert.False(figure.Away);
        Assert.Contains(Drain(b), m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome);
        TickTimes(game, LingerTicks + 5);
        Assert.NotNull(Seen(observer, "acc"));
    }

    [Fact] // AK-23
    public void Rejoin_after_linger_starts_at_given_position()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (1, 2));
        game.Tick();
        game.Leave(a);
        TickTimes(game, LingerTicks);
        Assert.Single(game.Tick());

        game.Join("acc", "A", (30, 40));
        game.Tick();

        var figure = Seen(observer, "acc")!;
        Assert.Equal((30f, 40f), (figure.X, figure.Y));
        Assert.False(figure.Away);
    }

    [Fact] // AK-22, AK-15
    public void Force_leave_kicks_signed_out_and_removes_at_once()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();

        game.ForceLeave("acc", savePosition: true);
        var removed = game.Tick();

        Assert.Equal("acc", Assert.Single(removed).Id);
        var last = Drain(a).Last();
        Assert.Equal(ServerMessage.PayloadOneofCase.Kicked, last.PayloadCase);
        Assert.Equal(Kicked.Types.Reason.SignedOut, last.Kicked.Reason);
        Assert.True(a.Outbox.Reader.Completion.IsCompleted);
        Assert.Null(Seen(observer, "acc"));
    }

    [Fact] // AK-16
    public void Force_leave_without_save_returns_nothing()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();

        game.ForceLeave("acc", savePosition: false);

        Assert.Empty(game.Tick());
        Assert.Null(Seen(observer, "acc"));
        Assert.Equal(Kicked.Types.Reason.SignedOut, Drain(a).Last().Kicked.Reason);
    }

    [Fact] // AK-22
    public void Force_leave_removes_waiting_figure_at_once()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();
        game.Leave(a);
        game.Tick();

        game.ForceLeave("acc", savePosition: true);

        Assert.Equal("acc", Assert.Single(game.Tick()).Id);
        Assert.Null(Seen(observer, "acc"));
        TickTimes(game, LingerTicks + 2);
    }

    [Fact]
    public void Force_leave_of_unknown_account_is_ignored()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        game.Tick();

        game.ForceLeave("fehlt", savePosition: true);

        Assert.Empty(game.Tick());
        Assert.False(observer.WasKicked);
        Assert.NotNull(Seen(observer, "obs"));
    }

    [Fact] // AK-20
    public void Snapshot_marks_only_waiting_figures_away()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", null);
        game.Tick();
        game.Leave(a);
        game.Tick();

        Assert.True(Seen(observer, "acc")!.Away);
        game.Tick();
        Assert.False(Seen(observer, "obs")!.Away);
    }
}
