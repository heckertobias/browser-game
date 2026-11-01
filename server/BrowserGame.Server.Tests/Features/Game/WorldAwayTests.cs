using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public class WorldAwayTests
{
    private static World NewWorld()
    {
        var world = new World(speed: 100, tickRateHz: 20, new Random(1));
        world.AddPlayer("p1", "Test", 0, 0, 0);
        return world;
    }

    [Fact] // AK-20
    public void Away_player_is_marked_and_keeps_position()
    {
        var world = NewWorld();
        var player = world.GetPlayer("p1")!;
        (player.X, player.Y) = (3, 4);

        world.SetAway("p1", 5);

        Assert.True(player.Away);
        Assert.Equal((3, 4), (player.X, player.Y));
        Assert.Contains(player, world.Players);
    }

    [Fact] // AK-20
    public void Away_player_does_not_move()
    {
        var world = NewWorld();
        var player = world.GetPlayer("p1")!;
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        Assert.True(player.X > 0);
        var x = player.X;

        world.SetAway("p1", 0);
        for (var i = 0; i < 5; i++)
        {
            world.Step();
        }

        Assert.Equal(x, player.X);
    }

    [Fact] // AK-20
    public void Away_player_ignores_inputs()
    {
        var world = NewWorld();
        world.SetAway("p1", 0);

        world.EnqueueInput("p1", new MoveInput(1, 1, 0));

        Assert.Empty(world.GetPlayer("p1")!.PendingInputs);
    }

    [Fact] // AK-20, AK-23
    public void Away_player_is_removed_after_linger()
    {
        var world = NewWorld();
        var player = world.GetPlayer("p1")!;
        (player.X, player.Y) = (7, 8);
        world.SetAway("p1", 10);

        Assert.Empty(world.RemoveExpiredAway(29, 20));
        Assert.Single(world.Players);

        var removed = Assert.Single(world.RemoveExpiredAway(30, 20));
        Assert.Same(player, removed);
        Assert.Equal((7, 8), (removed.X, removed.Y));
        Assert.Empty(world.Players);
    }

    [Fact] // AK-20
    public void Active_players_never_expire()
    {
        var world = NewWorld();

        Assert.Empty(world.RemoveExpiredAway(1_000_000, 20));
        Assert.Single(world.Players);
    }

    [Fact] // AK-21
    public void Cleared_away_player_is_active_again()
    {
        var world = NewWorld();
        var player = world.GetPlayer("p1")!;
        world.SetAway("p1", 0);

        world.ClearAway("p1");

        Assert.False(player.Away);
        Assert.Empty(world.RemoveExpiredAway(1000, 20));
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        Assert.True(player.X > 0);
    }

    [Fact]
    public void Away_of_unknown_player_is_ignored()
    {
        var world = NewWorld();

        world.SetAway("fehlt", 0);
        world.ClearAway("fehlt");

        Assert.Single(world.Players);
    }

    [Theory] // AK-20
    [InlineData(20, 400u)]
    [InlineData(0.5, 10u)]
    [InlineData(0.01, 1u)]
    [InlineData(0, 0u)]
    [InlineData(-5, 0u)]
    public void Seconds_are_rounded_up_to_ticks(double seconds, uint expected) =>
        Assert.Equal(expected, GameSettings.ToTicks(seconds));
}
