using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class WorldFacingTests
{
    private const int TickRate = 20;
    private const double Speed = 100;
    private const double StepDistance = Speed / TickRate;
    private const double StartFacing = 1.0;
    private const double MinInput = 0.5;
    private const double Tolerance = 1e-6;

    private static World NewWorld() =>
        new(Speed, TickRate, new Random(1), startFacing: StartFacing, minFacingInput: MinInput);

    private static (World World, Player Player) NewWorldWithPlayer()
    {
        var world = NewWorld();
        return (world, world.AddPlayer("p1", "Test", 0, 0, 0));
    }

    private static void Move(World world, uint seq, float dx, float dy)
    {
        world.EnqueueInput("p1", new MoveInput(seq, dx, dy));
        world.Step();
    }

    private static void Steps(World world, int count)
    {
        for (var i = 0; i < count; i++)
        {
            world.Step();
        }
    }

    [Theory] // AK-1
    [InlineData(1, 0)]
    [InlineData(0, -1)]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(0.6f, 0.8f)]
    public void Facing_points_in_direction_of_movement(float dx, float dy)
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, dx, dy);

        Assert.Equal(Math.Atan2(dy, dx), player.Facing, Tolerance);
    }

    [Fact] // AK-1
    public void Facing_uses_exact_angles()
    {
        foreach (var (dx, dy, expected) in new[] { (1f, 0f, 0.0), (0f, -1f, -Math.PI / 2), (-1f, 0f, Math.PI) })
        {
            var (world, player) = NewWorldWithPlayer();

            Move(world, 1, dx, dy);

            Assert.Equal(expected, player.Facing);
        }
    }

    [Theory] // AK-1, AK-2
    [InlineData(37)]
    [InlineData(-120)]
    [InlineData(5)]
    [InlineData(179)]
    [InlineData(-91)]
    public void Facing_is_a_free_angle(double degrees)
    {
        var (world, player) = NewWorldWithPlayer();
        var rad = degrees * Math.PI / 180;

        Move(world, 1, (float)Math.Cos(rad), (float)Math.Sin(rad));

        Assert.Equal(rad, player.Facing, Tolerance);
    }

    [Fact] // AK-2
    public void Facing_turns_with_new_direction()
    {
        var (world, player) = NewWorldWithPlayer();
        var rad = 37 * Math.PI / 180;

        Move(world, 1, 1, 0);
        Assert.Equal(0, player.Facing);

        Move(world, 2, (float)Math.Cos(rad), (float)Math.Sin(rad));
        Assert.Equal(rad, player.Facing, Tolerance);

        Move(world, 3, -1, -1);
        Assert.Equal(-3 * Math.PI / 4, player.Facing, Tolerance);
    }

    [Fact] // AK-3
    public void Facing_is_kept_after_coasting_stops()
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, 0, -1);
        Steps(world, World.MaxCoastTicks + 1);
        var y = player.Y;
        Steps(world, 2);

        Assert.Equal(y, player.Y);
        Assert.Equal(-Math.PI / 2, player.Facing);
    }

    [Fact] // AK-3
    public void Zero_input_keeps_facing()
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, -1, 0);
        Assert.Equal(Math.PI, player.Facing);

        Move(world, 2, 0, 0);
        Assert.Equal(Math.PI, player.Facing);
    }

    [Fact] // AK-4
    public void New_player_gets_start_facing()
    {
        var world = NewWorld();

        var random = world.AddPlayer("p2", "Test", 0);
        var placed = world.AddPlayer("p3", "Test", 0, 5, 5);

        Assert.Equal(StartFacing, random.Facing);
        Assert.Equal(StartFacing, placed.Facing);
    }

    [Fact] // AK-4
    public void Default_world_uses_planned_facing_values()
    {
        var world = new World(GameSettings.Speed, GameSettings.TickRateHz, new Random(1));

        Assert.Equal(Math.PI / 2, world.StartFacing);
        Assert.Equal(0.01, world.MinFacingInput);
        Assert.Equal(Math.PI / 2, world.AddPlayer("p1", "Test", 0, 0, 0).Facing);
        Assert.Equal(Math.PI / 2, GameSettings.DefaultFacing);
        Assert.Equal(0.01, GameSettings.MinFacingInput);
    }

    [Fact] // AK-4, AK-3
    public void Start_facing_is_kept_until_first_real_movement()
    {
        var (world, player) = NewWorldWithPlayer();

        Steps(world, 3);

        Assert.Equal(StartFacing, player.Facing);
    }

    [Fact] // AK-5
    public void Short_direction_does_not_change_facing()
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, 0.49f, 0);

        Assert.Equal(StartFacing, player.Facing);
        Assert.Equal(0.49 * StepDistance, player.X, 1e-5);

        Move(world, 2, 0.5f, 0);

        Assert.Equal(0, player.Facing);
    }

    [Fact] // AK-5
    public void Tiny_direction_with_default_threshold_does_not_change_facing()
    {
        var world = new World(Speed, TickRate, new Random(1));
        var player = world.AddPlayer("p1", "Test", 0, 0, 0);

        Move(world, 1, 0.005f, 0);
        Assert.Equal(World.DefaultFacing, player.Facing);

        Move(world, 2, -0.005f, 0.005f);
        Assert.Equal(World.DefaultFacing, player.Facing);
    }

    [Fact] // AK-9
    public void Facing_does_not_change_speed_or_vitals()
    {
        foreach (var facing in new[] { Math.PI, 0.3 })
        {
            var (world, player) = NewWorldWithPlayer();
            player.Facing = facing;

            Move(world, 1, 1, 0);

            Assert.Equal(StepDistance, player.X, Tolerance);
            Assert.Equal(0, player.Y);
            Assert.Equal(world.MaxHp, player.Hp);
            Assert.Equal(world.MaxMana, player.Mana);
        }
    }

    [Fact] // Plan-Entscheidung 2, AK-11
    public void Taking_over_waiting_player_keeps_facing()
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, -1, 0);
        world.SetAway("p1", 0);
        world.Step();
        world.ClearAway("p1");
        world.RestartInputs("p1");
        world.Step();

        Assert.Equal(Math.PI, player.Facing);
    }

    [Fact] // Plan-Entscheidung 3
    public void Respawn_keeps_facing()
    {
        var (world, player) = NewWorldWithPlayer();

        Move(world, 1, -1, 0);
        Assert.True(world.Damage("p1", world.MaxHp));

        Assert.Equal(1u, player.SpawnSeq);
        Assert.Equal(Math.PI, player.Facing);

        world.SetVitals("p1", hp: 0);

        Assert.Equal(Math.PI, player.Facing);
    }

    [Fact] // AK-1
    public void Waiting_player_does_not_turn()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetAway("p1", 0);
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();

        Assert.Equal(StartFacing, player.Facing);
    }

    [Fact] // AK-3, AK-5
    public void Standing_still_keeps_facing_even_with_zero_threshold()
    {
        var world = new World(Speed, TickRate, new Random(1), startFacing: StartFacing, minFacingInput: 0);
        var player = world.AddPlayer("p1", "Test", 0, 0, 0);

        world.Step();
        Assert.Equal(StartFacing, player.Facing);

        Move(world, 1, 0, 0);
        Assert.Equal(StartFacing, player.Facing);

        Move(world, 2, 0, -1);
        Assert.Equal(-Math.PI / 2, player.Facing);

        Steps(world, World.MaxCoastTicks + 2);
        Assert.Equal(-Math.PI / 2, player.Facing);

        Move(world, 3, float.NaN, 0);
        Assert.Equal(-Math.PI / 2, player.Facing);
    }
}
