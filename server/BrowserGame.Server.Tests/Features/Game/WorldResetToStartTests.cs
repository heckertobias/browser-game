using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class WorldResetToStartTests
{
    private const double MaxHp = 80;
    private const double MaxMana = 40;
    private const int TickRate = 20;
    private const double Far = 500; // weit außerhalb des SpawnRadius

    private static World NewWorld(int seed = 1) => new(speed: 100, tickRateHz: TickRate, new Random(seed), MaxHp, MaxMana);

    private static double DistanceFromOrigin(Player player) => Math.Sqrt(player.X * player.X + player.Y * player.Y);

    private static void Steps(World world, int count)
    {
        for (var i = 0; i < count; i++)
        {
            world.Step();
        }
    }

    [Fact] // 015 AK-7
    public void ResetToStart_moves_to_start_point_and_keeps_vitals()
    {
        var world = NewWorld();
        var player = world.AddPlayer("p1", "Test", 0, Far, Far);
        world.SetVitals("p1", 30, 5);

        world.ResetToStart(player);

        Assert.True(DistanceFromOrigin(player) <= World.SpawnRadius);
        Assert.Equal(30, player.Hp);
        Assert.Equal(5, player.Mana);
        Assert.Equal(1u, player.SpawnSeq);
    }

    [Fact] // 015 AK-7
    public void ResetToStart_uses_start_point_like_a_new_player()
    {
        var worldA = NewWorld(7);
        var reset = worldA.AddPlayer("p1", "Test", 0, Far, Far);
        worldA.ResetToStart(reset);
        var fresh = NewWorld(7).AddPlayer("p1", "Test", 0);

        Assert.Equal((fresh.X, fresh.Y), (reset.X, reset.Y));
    }

    [Fact] // 015 AK-7
    public void ResetToStart_drops_direction_and_pending_inputs()
    {
        var world = NewWorld();
        var player = world.AddPlayer("p1", "Test", 0, Far, Far);
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        world.EnqueueInput("p1", new MoveInput(2, 1, 0));
        world.EnqueueInput("p1", new MoveInput(3, 1, 0));

        world.ResetToStart(player);

        Assert.Empty(player.PendingInputs);
        Assert.Equal(3u, player.LastProcessedSeq);
        Assert.Equal((0, 0), (player.DirX, player.DirY));
        var (x, y) = (player.X, player.Y);
        Steps(world, World.MaxCoastTicks + 2);
        Assert.Equal((x, y), (player.X, player.Y));
    }

    [Fact] // 015 AK-7
    public void Each_reset_increments_spawn_seq()
    {
        var world = NewWorld();
        var player = world.AddPlayer("p1", "Test", 0, Far, Far);

        world.ResetToStart(player);
        world.ResetToStart(player);

        Assert.Equal(2u, player.SpawnSeq);
    }

    [Fact] // 015 AK-11
    public void ResetToStart_does_not_touch_other_players()
    {
        var world = NewWorld();
        var p1 = world.AddPlayer("p1", "Test", 0, Far, Far);
        var p2 = world.AddPlayer("p2", "Other", 0, 10, 20);
        world.SetVitals("p2", 30, 7);

        world.ResetToStart(p1);

        Assert.Equal((10, 20), (p2.X, p2.Y));
        Assert.Equal(30, p2.Hp);
        Assert.Equal(7, p2.Mana);
        Assert.Equal(0u, p2.SpawnSeq);
    }
}
