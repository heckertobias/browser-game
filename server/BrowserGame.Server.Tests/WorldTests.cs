using BrowserGame.Server.Game;

namespace BrowserGame.Server.Tests;

public class WorldTests
{
    private const double Speed = 200;
    private const int TickRate = 20;
    private const double StepDistance = Speed / TickRate;
    private const double Tolerance = 1e-6;

    private static (World World, Player Player) CreateWorldWithPlayer()
    {
        var world = new World(Speed, TickRate, new Random(42));
        var player = world.AddPlayer("p1", "Test", 0xff0000);
        (player.X, player.Y) = (0, 0);
        return (world, player);
    }

    [Theory] // AK-2
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    public void Straight_input_moves_one_step_distance(float dx, float dy)
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, dx, dy));
        world.Step();

        Assert.Equal(dx * StepDistance, player.X, Tolerance);
        Assert.Equal(dy * StepDistance, player.Y, Tolerance);
        Assert.Equal(1u, player.LastProcessedSeq);
    }

    [Fact] // AK-2: diagonal genauso schnell wie geradeaus
    public void Diagonal_input_is_as_fast_as_straight()
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, 1, 1));
        world.Step();

        Assert.Equal(StepDistance, Math.Sqrt(player.X * player.X + player.Y * player.Y), Tolerance);
    }

    [Fact] // AK-2: freie Richtungen, nicht nur 8 Kacheln-Richtungen
    public void Arbitrary_direction_is_kept()
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, 0.3f, 0.4f));
        world.Step();

        Assert.Equal(0.3 * StepDistance, player.X, 1e-5);
        Assert.Equal(0.4 * StepDistance, player.Y, 1e-5);
    }

    [Fact] // AK-5
    public void Oversized_direction_is_clamped_to_speed()
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, 10, 0));
        world.Step();

        Assert.Equal(StepDistance, player.X, Tolerance);
    }

    [Theory] // AK-5
    [InlineData(float.NaN, 0)]
    [InlineData(float.PositiveInfinity, 0)]
    [InlineData(0, float.NegativeInfinity)]
    public void Invalid_direction_means_standing_still(float dx, float dy)
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, dx, dy));
        world.Step();

        Assert.Equal(0, player.X);
        Assert.Equal(0, player.Y);
    }

    [Fact] // AK-5: Eingabe-Flut bringt keine zusätzliche Strecke
    public void Input_flood_does_not_increase_distance_per_tick()
    {
        var (world, player) = CreateWorldWithPlayer();

        for (uint seq = 1; seq <= 50; seq++)
        {
            world.EnqueueInput("p1", new MoveInput(seq, 1, 0));
        }

        world.Step();

        Assert.Equal(StepDistance, player.X, Tolerance);
        Assert.Equal(World.MaxQueuedInputs - 1, player.PendingInputs.Count);
    }

    [Fact] // AK-5: wiederholte oder alte Sequenznummern werden ignoriert
    public void Replayed_inputs_are_ignored()
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(5, 1, 0));
        world.EnqueueInput("p1", new MoveInput(5, 1, 0));
        world.EnqueueInput("p1", new MoveInput(3, 1, 0));

        Assert.Single(player.PendingInputs);
    }

    [Fact]
    public void Without_input_player_coasts_briefly_then_stops()
    {
        var (world, player) = CreateWorldWithPlayer();

        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        for (var i = 0; i < 10; i++)
        {
            world.Step();
        }

        Assert.Equal((1 + World.MaxCoastTicks) * StepDistance, player.X, Tolerance);
    }

    [Fact] // AK-7
    public void World_has_no_borders()
    {
        var (world, player) = CreateWorldWithPlayer();
        (player.X, player.Y) = (100_000, -100_000);

        world.EnqueueInput("p1", new MoveInput(1, 1, -1));
        world.Step();

        Assert.True(player.X > 100_000);
        Assert.True(player.Y < -100_000);
    }

    [Fact]
    public void Spawn_is_near_origin()
    {
        var world = new World(Speed, TickRate, new Random(1));

        for (var i = 0; i < 100; i++)
        {
            var player = world.AddPlayer($"p{i}", "Test", 0);
            Assert.True(Math.Sqrt(player.X * player.X + player.Y * player.Y) <= World.SpawnRadius);
        }
    }

    [Fact] // AK-4
    public void Removed_player_is_gone()
    {
        var (world, _) = CreateWorldWithPlayer();

        Assert.True(world.RemovePlayer("p1"));
        Assert.Empty(world.Players);
        world.EnqueueInput("p1", new MoveInput(1, 1, 0)); // darf nicht werfen
    }
}
