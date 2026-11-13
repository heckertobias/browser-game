using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CombatDummyTests
{
    private const int TickRate = 20;
    private const double DummyX = 10;
    private const double DummyY = -20;
    private const double MaxHp = 50;
    private const double RespawnSeconds = 2;
    private const uint RespawnTicks = 40; // RespawnSeconds * TickRate
    private const uint Start = 1000;

    private static Combat NewCombat(double respawnSeconds = RespawnSeconds) =>
        new(TickRate, dummyX: DummyX, dummyY: DummyY, dummyMaxHp: MaxHp, dummyRespawnSeconds: respawnSeconds);

    private static Target Dummy(Combat combat) => combat.GetTarget(Combat.DummyId)!;

    [Fact] // AK-13
    public void New_combat_has_exactly_one_dummy_with_full_hp()
    {
        var combat = NewCombat();
        var dummy = Assert.Single(combat.Targets);

        Assert.Equal(Combat.DummyId, dummy.Id);
        Assert.Equal(TargetState.Types.Kind.TrainingDummy, dummy.Kind);
        Assert.Equal(DummyX, dummy.X);
        Assert.Equal(DummyY, dummy.Y);
        Assert.Equal(MaxHp, dummy.Hp);
        Assert.Equal(MaxHp, dummy.MaxHp);
        Assert.True(dummy.IsAlive);
        Assert.Null(dummy.RespawnAtTick);
        Assert.Same(dummy, combat.GetTarget(Combat.DummyId));
        Assert.Same(dummy, Assert.Single(combat.AliveTargets));
    }

    [Fact] // AK-13
    public void Default_combat_uses_game_settings()
    {
        var combat = new Combat(GameSettings.TickRateHz);
        var dummy = Dummy(combat);

        Assert.Equal(GameSettings.DummyX, dummy.X);
        Assert.Equal(GameSettings.DummyY, dummy.Y);
        Assert.Equal(GameSettings.DummyMaxHp, dummy.Hp);
        Assert.Equal(GameSettings.DummyMaxHp, dummy.MaxHp);
        Assert.Equal(GameSettings.ToTicks(GameSettings.DummyRespawnSeconds), combat.DummyRespawnTicks);
        Assert.Equal(GameSettings.ToTicks(GameSettings.GlobalCooldownSeconds), combat.GlobalCooldownTicks);
        Assert.Equal(GameSettings.AttackArcDegrees, combat.AttackArcDegrees);
        Assert.Equal(GameSettings.MeleeRange, combat.MeleeRange);
        Assert.Equal(GameSettings.MeleeDamage, combat.MeleeDamage);
        Assert.Equal(GameSettings.RangedRange, combat.RangedRange);
        Assert.Equal(GameSettings.RangedDamage, combat.RangedDamage);
        Assert.Equal(GameSettings.RangedManaCost, combat.RangedManaCost);
        Assert.Equal(GameSettings.ProjectileSpeed, combat.ProjectileSpeed);
    }

    [Fact] // AK-13
    public void Default_dummy_stands_outside_the_spawn_radius()
    {
        var distance = Math.Sqrt(GameSettings.DummyX * GameSettings.DummyX + GameSettings.DummyY * GameSettings.DummyY);

        Assert.True(distance > World.SpawnRadius);
    }

    [Fact] // AK-15
    public void Default_delays_match_the_plan_in_ticks()
    {
        var combat = new Combat(20);

        Assert.Equal(100u, combat.DummyRespawnTicks);
        Assert.Equal(20u, combat.GlobalCooldownTicks);
    }

    [Theory] // AK-15
    [InlineData(0.01, 1u)]
    [InlineData(0, 0u)]
    [InlineData(-1, 0u)]
    [InlineData(2, 40u)]
    public void Delays_round_up_to_whole_ticks(double seconds, uint ticks)
    {
        var combat = new Combat(TickRate, globalCooldownSeconds: seconds, dummyRespawnSeconds: seconds);

        Assert.Equal(ticks, combat.DummyRespawnTicks);
        Assert.Equal(ticks, combat.GlobalCooldownTicks);
    }

    [Fact] // AK-13
    public void Unknown_target_id_returns_null()
    {
        Assert.Null(NewCombat().GetTarget("fehlt"));
    }

    [Fact] // AK-15
    public void Damage_lowers_hp_and_stops_at_zero()
    {
        var combat = NewCombat();
        var dummy = Dummy(combat);

        Combat.Damage(dummy, 20);
        Assert.Equal(30, dummy.Hp);
        Assert.True(dummy.IsAlive);

        Combat.Damage(dummy, 1000);
        Assert.Equal(0, dummy.Hp);
        Assert.False(dummy.IsAlive);
        Assert.Empty(combat.AliveTargets);
        Assert.Single(combat.Targets);
    }

    [Theory] // AK-15
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(double.NaN)]
    public void Non_positive_or_invalid_damage_is_ignored(double amount)
    {
        var dummy = Dummy(NewCombat());

        Combat.Damage(dummy, amount);

        Assert.Equal(MaxHp, dummy.Hp);
        Assert.True(dummy.IsAlive);
    }

    [Fact] // AK-15
    public void Dead_dummy_returns_with_full_hp_after_the_delay()
    {
        var combat = NewCombat();
        var dummy = Dummy(combat);
        Combat.Damage(dummy, MaxHp);

        combat.Step(Start);
        Assert.Equal(Start + RespawnTicks, dummy.RespawnAtTick);

        for (var tick = Start + 1; tick < Start + RespawnTicks; tick++)
        {
            combat.Step(tick);
            Assert.Equal(0, dummy.Hp);
            Assert.False(dummy.IsAlive);
            Assert.Empty(combat.AliveTargets);
        }

        combat.Step(Start + RespawnTicks);
        Assert.Equal(MaxHp, dummy.Hp);
        Assert.Null(dummy.RespawnAtTick);
        Assert.True(dummy.IsAlive);
        Assert.Same(dummy, Assert.Single(combat.AliveTargets));
        Assert.Equal(Combat.DummyId, dummy.Id);
        Assert.Equal(DummyX, dummy.X);
        Assert.Equal(DummyY, dummy.Y);
    }

    [Fact] // AK-15
    public void Damage_while_dead_does_not_delay_the_respawn()
    {
        var combat = NewCombat();
        var dummy = Dummy(combat);
        Combat.Damage(dummy, MaxHp);
        combat.Step(Start);

        Combat.Damage(dummy, 10);
        for (var tick = Start + 1; tick < Start + RespawnTicks; tick++)
        {
            combat.Step(tick);
            Assert.Equal(0, dummy.Hp);
            Assert.Equal(Start + RespawnTicks, dummy.RespawnAtTick);
        }

        combat.Step(Start + RespawnTicks);
        Assert.Equal(MaxHp, dummy.Hp);
        Assert.True(dummy.IsAlive);
    }

    [Fact] // AK-15
    public void Respawned_dummy_can_die_again()
    {
        var combat = NewCombat();
        var dummy = Dummy(combat);
        Combat.Damage(dummy, MaxHp);
        combat.Step(Start);
        var back = Start + RespawnTicks;
        combat.Step(back);
        Assert.True(dummy.IsAlive);

        Combat.Damage(dummy, MaxHp);
        combat.Step(back + 1);
        Assert.Equal(back + 1 + RespawnTicks, dummy.RespawnAtTick);

        combat.Step(back + 1 + RespawnTicks);
        Assert.Equal(MaxHp, dummy.Hp);
        Assert.True(dummy.IsAlive);
    }

    [Fact] // AK-15
    public void Living_dummy_keeps_its_hp_over_steps()
    {
        var combat = NewCombat();
        var dummy = Dummy(combat);
        Combat.Damage(dummy, 30);

        for (uint tick = 1; tick <= 3 * RespawnTicks; tick++)
        {
            combat.Step(tick);
        }

        Assert.Equal(MaxHp - 30, dummy.Hp);
        Assert.Null(dummy.RespawnAtTick);
    }

    [Fact] // AK-15
    public void Zero_delay_returns_in_the_same_step()
    {
        var combat = NewCombat(respawnSeconds: 0);
        var dummy = Dummy(combat);
        Combat.Damage(dummy, MaxHp);

        combat.Step(Start);

        Assert.True(dummy.IsAlive);
        Assert.Equal(MaxHp, dummy.Hp);
    }

    [Fact] // AK-16
    public void Dummy_never_harms_a_player_next_to_it()
    {
        var world = new World(100, TickRate, new Random(1));
        var player = world.AddPlayer("p1", "Test", 0, DummyX + 10, DummyY);
        var combat = NewCombat();

        for (uint tick = 1; tick <= 3 * RespawnTicks; tick++)
        {
            if (tick == 5)
            {
                Combat.Damage(Dummy(combat), MaxHp);
            }

            world.Step();
            combat.Step(tick);
        }

        Assert.Equal(world.MaxHp, player.Hp);
        Assert.Equal(0u, player.SpawnSeq);
        Assert.Equal(DummyX + 10, player.X);
        Assert.Equal(DummyY, player.Y);
    }

    [Fact] // AK-17
    public void Each_combat_starts_with_a_fresh_dummy()
    {
        var first = NewCombat();
        Combat.Damage(Dummy(first), MaxHp);
        first.Step(Start);

        var second = NewCombat();

        Assert.Equal(MaxHp, Dummy(second).Hp);
        Assert.True(Dummy(second).IsAlive);
        Assert.Null(Dummy(second).RespawnAtTick);
        Assert.NotSame(Dummy(first), Dummy(second));
    }
}
