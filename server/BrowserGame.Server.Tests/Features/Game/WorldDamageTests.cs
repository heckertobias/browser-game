using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class WorldDamageTests
{
    private const double MaxHp = 80;
    private const double MaxMana = 40;
    private const double RegenPerSecond = 10;
    private const int TickRate = 20;
    private const double RegenPerTick = RegenPerSecond / TickRate; // 0,5
    private const double Far = 500; // weit außerhalb des SpawnRadius

    private static World NewWorld(int seed = 1) =>
        new(speed: 100, tickRateHz: TickRate, new Random(seed), MaxHp, MaxMana, RegenPerSecond);

    private static (World World, Player Player) NewWorldWithPlayer()
    {
        var world = NewWorld();
        return (world, world.AddPlayer("p1", "Test", 0, Far, Far));
    }

    private static double DistanceFromOrigin(Player player) => Math.Sqrt(player.X * player.X + player.Y * player.Y);

    private static void AssertRespawned(Player player, uint spawnSeq)
    {
        Assert.True(DistanceFromOrigin(player) <= World.SpawnRadius);
        Assert.Equal(MaxHp, player.Hp);
        Assert.Equal(MaxMana, player.Mana);
        Assert.Equal(spawnSeq, player.SpawnSeq);
    }

    private static void Steps(World world, int count)
    {
        for (var i = 0; i < count; i++)
        {
            world.Step();
        }
    }

    [Fact] // AK-7, AK-10
    public void Damage_lowers_hp_by_amount()
    {
        var (world, player) = NewWorldWithPlayer();

        var died = world.Damage("p1", 30);

        Assert.False(died);
        Assert.Equal(MaxHp - 30, player.Hp);
        Assert.Equal(MaxMana, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
        Assert.Equal((Far, Far), (player.X, player.Y));
    }

    [Fact] // AK-7
    public void Damage_adds_up()
    {
        var (world, player) = NewWorldWithPlayer();

        Assert.False(world.Damage("p1", 30));
        Assert.False(world.Damage("p1", 30));

        Assert.Equal(20, player.Hp);
        Assert.Equal(0u, player.SpawnSeq);
    }

    [Fact] // AK-7, AK-8, AK-9
    public void Damage_to_exactly_zero_kills_and_respawns()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 30;
        player.Mana = 5;

        Assert.True(world.Damage("p1", 30));

        AssertRespawned(player, 1);
    }

    [Fact] // AK-7, AK-8, AK-9
    public void Damage_below_zero_kills_and_respawns_immediately()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 20;
        player.Mana = 5;

        Assert.True(world.Damage("p1", 30));

        AssertRespawned(player, 1);
        Assert.True(player.Hp > 0);
    }

    [Fact] // AK-7, AK-8
    public void Damage_of_full_hp_kills_once()
    {
        var (world, player) = NewWorldWithPlayer();

        Assert.True(world.Damage("p1", MaxHp + 1000));

        AssertRespawned(player, 1);
    }

    [Theory] // AK-7, AK-1
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Non_positive_or_invalid_damage_is_ignored(double amount)
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 50;

        Assert.False(world.Damage("p1", amount));

        Assert.Equal(50, player.Hp);
        Assert.Equal(0u, player.SpawnSeq);
        Assert.Equal((Far, Far), (player.X, player.Y));
    }

    [Fact] // AK-7
    public void Damage_to_unknown_player_is_ignored()
    {
        var (world, player) = NewWorldWithPlayer();

        Assert.False(world.Damage("fehlt", 30));

        Assert.Equal(MaxHp, player.Hp);
        Assert.Equal(0u, player.SpawnSeq);
    }

    [Fact] // AK-8
    public void Respawn_uses_start_point_like_a_new_player()
    {
        var a = NewWorld(7);
        var dead = a.AddPlayer("p1", "Test", 0, Far, Far);
        a.Damage("p1", MaxHp);
        var b = NewWorld(7);
        var fresh = b.AddPlayer("p1", "Test", 0);

        Assert.Equal(fresh.X, dead.X);
        Assert.Equal(fresh.Y, dead.Y);
        Assert.True(DistanceFromOrigin(dead) <= World.SpawnRadius);
    }

    [Fact] // AK-8
    public void Respawn_drops_direction_and_pending_inputs()
    {
        var (world, player) = NewWorldWithPlayer();
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        world.EnqueueInput("p1", new MoveInput(2, 1, 0));
        world.EnqueueInput("p1", new MoveInput(3, 1, 0));

        world.Damage("p1", MaxHp);

        Assert.Empty(player.PendingInputs);
        Assert.Equal(3u, player.LastProcessedSeq);
        Assert.Equal(0, player.DirX);
        Assert.Equal(0, player.DirY);
        var (x, y) = (player.X, player.Y);
        Steps(world, World.MaxCoastTicks + 2);
        Assert.Equal((x, y), (player.X, player.Y));
    }

    [Fact] // AK-8
    public void Inputs_after_respawn_continue_with_next_seq()
    {
        var (world, player) = NewWorldWithPlayer();
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.EnqueueInput("p1", new MoveInput(2, 1, 0));
        world.Damage("p1", MaxHp);
        var (x, y) = (player.X, player.Y);

        world.EnqueueInput("p1", new MoveInput(2, 0, 1));
        Assert.Empty(player.PendingInputs);

        world.EnqueueInput("p1", new MoveInput(3, 1, 0));
        world.Step();

        Assert.Equal(3u, player.LastProcessedSeq);
        Assert.Equal(x + world.StepDistance, player.X, 1e-9);
        Assert.Equal(y, player.Y, 1e-9);
    }

    [Fact] // AK-8
    public void Each_death_increments_spawn_seq()
    {
        var (world, player) = NewWorldWithPlayer();

        world.Damage("p1", MaxHp);
        world.SetVitals("p1", hp: 0);

        Assert.Equal(2u, player.SpawnSeq);
        Assert.Equal(MaxHp, player.Hp);
    }

    [Fact] // AK-8
    public void Respawn_does_not_touch_other_players()
    {
        var (world, _) = NewWorldWithPlayer();
        var other = world.AddPlayer("p2", "Test", 0, 10, 20);
        other.Hp = 30;
        other.Mana = 7;

        world.Damage("p1", MaxHp);

        Assert.Equal((10.0, 20.0), (other.X, other.Y));
        Assert.Equal(30, other.Hp);
        Assert.Equal(7, other.Mana);
        Assert.Equal(0u, other.SpawnSeq);
    }

    [Fact] // AK-8
    public void Waiting_player_respawns_and_stays_waiting()
    {
        var (world, player) = NewWorldWithPlayer();
        world.EnqueueInput("p1", new MoveInput(5, 1, 0));
        world.SetAway("p1", 0);

        world.Damage("p1", MaxHp);

        Assert.True(player.Away);
        AssertRespawned(player, 1);

        world.ClearAway("p1");
        world.RestartInputs("p1");

        Assert.Equal(0u, player.LastProcessedSeq);
        AssertRespawned(player, 1);
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();
        Assert.Equal(1u, player.LastProcessedSeq);
    }

    [Fact] // AK-10, AK-7
    public void SetVitals_sets_hp_and_mana()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: 25, mana: 10);

        Assert.Equal(25, player.Hp);
        Assert.Equal(10, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
        Assert.Equal((Far, Far), (player.X, player.Y));
    }

    [Fact] // AK-10
    public void SetVitals_null_keeps_the_other_value()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: 30);
        Assert.Equal(30, player.Hp);
        Assert.Equal(MaxMana, player.Mana);

        world.SetVitals("p1", mana: 5);
        Assert.Equal(30, player.Hp);
        Assert.Equal(5, player.Mana);
    }

    [Fact] // AK-1, AK-10
    public void SetVitals_caps_values_at_max()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: 500, mana: 500);

        Assert.Equal(MaxHp, player.Hp);
        Assert.Equal(MaxMana, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
    }

    [Fact] // AK-1, AK-10
    public void SetVitals_caps_mana_at_zero()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: 30, mana: -5);

        Assert.Equal(0, player.Mana);
        Assert.Equal(30, player.Hp);
        Assert.Equal(0u, player.SpawnSeq);
    }

    [Theory] // AK-1, AK-8, AK-9, AK-10
    [InlineData(0)]
    [InlineData(-5)]
    public void SetVitals_hp_at_or_below_zero_respawns(double hp)
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: hp, mana: 10);

        AssertRespawned(player, 1);
    }

    [Fact] // AK-10
    public void SetVitals_of_unknown_player_is_ignored()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("fehlt", hp: 0, mana: 0);

        Assert.Equal(MaxHp, player.Hp);
        Assert.Equal(MaxMana, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
    }

    [Fact] // AK-1, AK-10
    public void SetVitals_ignores_nan()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 30;
        player.Mana = 10;

        world.SetVitals("p1", hp: double.NaN, mana: double.NaN);

        Assert.Equal(30, player.Hp);
        Assert.Equal(10, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
        Assert.Equal((Far, Far), (player.X, player.Y));
    }

    [Fact] // AK-10, AK-3, AK-4
    public void Set_values_regenerate_mana_but_not_hp()
    {
        var (world, player) = NewWorldWithPlayer();

        world.SetVitals("p1", hp: 50, mana: 0);
        Steps(world, TickRate);

        Assert.Equal(TickRate * RegenPerTick, player.Mana, 1e-9);
        Assert.Equal(50, player.Hp);
    }
}
