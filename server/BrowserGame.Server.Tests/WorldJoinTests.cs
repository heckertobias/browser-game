using BrowserGame.Server.Game;

namespace BrowserGame.Server.Tests;

public sealed class WorldJoinTests
{
    private static World NewWorld() => new(speed: 100, tickRateHz: 20, new Random(1));

    [Fact] // AK-18
    public void Player_is_added_at_given_position()
    {
        var player = NewWorld().AddPlayer("p1", "Test", 0, 1234.5, -678.25);

        Assert.Equal(1234.5, player.X);
        Assert.Equal(-678.25, player.Y);
    }

    [Fact]
    public void Restarted_inputs_accept_sequence_from_one()
    {
        var world = NewWorld();
        var player = world.AddPlayer("p1", "Test", 0, 0, 0);
        world.EnqueueInput("p1", new MoveInput(100, 0, 0));
        world.Step();

        world.RestartInputs("p1");
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();

        Assert.Equal(world.StepDistance, player.X, 9);
        Assert.Equal(1u, player.LastProcessedSeq);
    }

    [Fact]
    public void Restarted_inputs_drop_pending_and_stop()
    {
        var world = NewWorld();
        var player = world.AddPlayer("p1", "Test", 0, 0, 0);
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        var x = player.X;

        world.EnqueueInput("p1", new MoveInput(2, 1, 0));
        world.RestartInputs("p1");
        world.Step();

        Assert.Equal(x, player.X);
        Assert.Empty(player.PendingInputs);
    }

    [Fact]
    public void Restart_of_unknown_player_is_ignored() => NewWorld().RestartInputs("fehlt");
}
