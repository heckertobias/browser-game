using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class WorldVitalsTests
{
    private const double MaxHp = 80;
    private const double MaxMana = 40;
    private const double RegenPerSecond = 10;
    private const int TickRate = 20;
    private const double RegenPerTick = RegenPerSecond / TickRate; // 0,5

    private static (World World, Player Player) NewWorldWithPlayer()
    {
        var world = new World(speed: 100, tickRateHz: TickRate, new Random(1), MaxHp, MaxMana, RegenPerSecond);
        return (world, world.AddPlayer("p1", "Test", 0, 0, 0));
    }

    private static void Steps(World world, int count)
    {
        for (var i = 0; i < count; i++)
        {
            world.Step();
        }
    }

    [Fact] // AK-2, AK-1
    public void New_player_starts_with_full_hp_and_mana()
    {
        var (world, _) = NewWorldWithPlayer();
        var random = world.AddPlayer("p2", "Test", 0);
        var placed = world.AddPlayer("p3", "Test", 0, 5, 5);

        foreach (var player in new[] { random, placed })
        {
            Assert.Equal(MaxHp, player.Hp);
            Assert.Equal(MaxMana, player.Mana);
            Assert.Equal(0u, player.SpawnSeq);
        }
    }

    [Fact] // AK-1, AK-2, AK-3
    public void Default_world_uses_planned_values()
    {
        var world = new World(GameSettings.Speed, GameSettings.TickRateHz, new Random(1));
        var player = world.AddPlayer("p1", "Test", 0);

        Assert.Equal(100, player.Hp, 1e-9);
        Assert.Equal(100, player.Mana, 1e-9);

        player.Mana = 0;
        Steps(world, GameSettings.TickRateHz);

        Assert.Equal(5, player.Mana, 1e-9);
    }

    [Fact] // AK-3
    public void Mana_regenerates_each_tick()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Mana = 0;

        Steps(world, 1);
        Assert.Equal(RegenPerTick, player.Mana, 1e-9);

        Steps(world, 9);
        Assert.Equal(10 * RegenPerTick, player.Mana, 1e-9);
    }

    [Fact] // AK-3, AK-1
    public void Mana_stops_at_max()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Mana = MaxMana - 0.2;

        Steps(world, 1);
        Assert.Equal(MaxMana, player.Mana);

        Steps(world, 5);
        Assert.Equal(MaxMana, player.Mana);
    }

    [Fact] // AK-1, AK-3
    public void Mana_from_empty_stays_within_bounds_until_full()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Mana = 0;

        for (var i = 0; i < 200; i++)
        {
            world.Step();
            Assert.InRange(player.Mana, 0, MaxMana);
            Assert.InRange(player.Hp, 0, MaxHp);
        }

        Assert.Equal(MaxMana, player.Mana);
    }

    [Fact] // AK-1
    public void Mana_above_max_is_capped_on_next_tick()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Mana = MaxMana + 60;

        Steps(world, 1);

        Assert.Equal(MaxMana, player.Mana);
    }

    [Fact] // AK-4
    public void Hp_does_not_regenerate()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 50;
        player.Mana = 0;
        world.EnqueueInput("p1", new MoveInput(1, 1, 0));

        Steps(world, 100);

        Assert.Equal(50, player.Hp);
        Assert.Equal(MaxMana, player.Mana);
    }

    [Fact] // AK-3
    public void Waiting_player_regenerates_mana()
    {
        var (world, player) = NewWorldWithPlayer();
        world.SetAway("p1", 0);
        player.Mana = 0;

        Steps(world, 4);

        Assert.Equal(4 * RegenPerTick, player.Mana, 1e-9);
        Assert.Equal(MaxHp, player.Hp);
    }

    [Fact] // AK-2
    public void Taking_over_waiting_player_keeps_hp_and_mana()
    {
        var (world, player) = NewWorldWithPlayer();
        player.Hp = 30;
        player.Mana = 10;

        world.SetAway("p1", 0);
        world.ClearAway("p1");
        world.RestartInputs("p1");

        Assert.Equal(30, player.Hp);
        Assert.Equal(10, player.Mana);
        Assert.Equal(0u, player.SpawnSeq);
    }
}
