using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CombatAttackTests
{
    private const int TickRate = 20;
    private const double MeleeRange = 32;
    private const double MeleeDamage = 10;
    private const double RangedRange = 160;
    private const double RangedDamage = 15;
    private const double ManaCost = 20;
    private const double ProjectileSpeed = 400;
    private const double Step = ProjectileSpeed / TickRate;
    private const uint CooldownTicks = 20;
    private const double DummyMaxHp = 100;
    private const uint T = 1000;
    private const double Tolerance = 1e-9;

    private static Combat NewCombat(double dummyMaxHp = DummyMaxHp) => new(
        TickRate, attackArcDegrees: 90, meleeRange: MeleeRange, meleeDamage: MeleeDamage, rangedRange: RangedRange,
        rangedDamage: RangedDamage, rangedManaCost: ManaCost, projectileSpeed: ProjectileSpeed, globalCooldownSeconds: 1,
        dummyX: 0, dummyY: 0, dummyMaxHp: dummyMaxHp, dummyRespawnSeconds: 5);

    private static Player NewPlayer(double x, double y, double facing = 0, double mana = 100, string id = "p1") =>
        new(id, "Test", 0, x, y) { Hp = 100, Mana = mana, Facing = facing };

    private static Target Dummy(Combat combat) => combat.GetTarget(Combat.DummyId)!;

    private static void AssertUnchanged(Combat combat, Player player, double mana, double facing, double hp = DummyMaxHp)
    {
        Assert.Equal(hp, Dummy(combat).Hp);
        Assert.Equal(mana, player.Mana);
        Assert.Equal(facing, player.Facing);
        Assert.Equal(0u, player.NextAttackTick);
        Assert.Empty(combat.Projectiles);
    }

    [Theory] // AK-2, AK-12
    [InlineData(Attack.Types.Kind.Melee, "")]
    [InlineData(Attack.Types.Kind.Melee, "fehlt")]
    [InlineData(Attack.Types.Kind.Ranged, "")]
    [InlineData(Attack.Types.Kind.Ranged, "fehlt")]
    public void Unknown_target_does_nothing(Attack.Types.Kind kind, string targetId)
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);

        Assert.Equal(AttackResult.InvalidTarget, combat.TryAttack(player, kind, targetId, T));
        AssertUnchanged(combat, player, 100, 0);
    }

    [Theory] // AK-2
    [InlineData(Attack.Types.Kind.Melee)]
    [InlineData(Attack.Types.Kind.Ranged)]
    public void Dead_target_cannot_be_attacked(Attack.Types.Kind kind)
    {
        var combat = NewCombat();
        Combat.Damage(Dummy(combat), DummyMaxHp);
        var player = NewPlayer(-10, 0);

        Assert.Equal(AttackResult.InvalidTarget, combat.TryAttack(player, kind, Combat.DummyId, T));
        AssertUnchanged(combat, player, 100, 0, hp: 0);
    }

    [Theory] // AK-2, AK-12
    [InlineData(0)]
    [InlineData(99)]
    public void Unknown_kind_does_nothing(int kind)
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);

        Assert.Equal(AttackResult.InvalidKind, combat.TryAttack(player, (Attack.Types.Kind)kind, Combat.DummyId, T));
        AssertUnchanged(combat, player, 100, 0);
    }

    [Fact] // AK-4, AK-14
    public void Melee_in_range_damages_immediately()
    {
        var combat = NewCombat();
        var player = NewPlayer(-MeleeRange, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(DummyMaxHp - MeleeDamage, Dummy(combat).Hp);
        Assert.Empty(combat.Projectiles);
    }

    [Fact] // AK-5, AK-12
    public void Melee_out_of_range_does_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-MeleeRange - 1, 0);

        Assert.Equal(AttackResult.OutOfRange, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));
        AssertUnchanged(combat, player, 100, 0);
    }

    [Theory] // AK-6
    [InlineData(100)]
    [InlineData(0)]
    public void Melee_costs_no_mana(double mana)
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0, mana: mana);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(mana, player.Mana);
        Assert.Equal(DummyMaxHp - MeleeDamage, Dummy(combat).Hp);
    }

    [Fact] // AK-7, AK-14
    public void Ranged_hits_only_on_impact()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        var projectile = Assert.Single(combat.Projectiles);
        Assert.Equal(-100, projectile.X);
        Assert.Equal(0, projectile.Y);
        Assert.Equal(Combat.DummyId, projectile.TargetId);
        Assert.Equal(RangedDamage, projectile.Damage);
        Assert.NotEqual(0u, projectile.Id);
        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);

        combat.Step(T);
        Assert.Equal(-100 + Step, projectile.X, Tolerance);
        Assert.Equal(0, projectile.Y, Tolerance);
        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);

        for (var i = 1u; i <= 3; i++)
        {
            combat.Step(T + i);
        }

        Assert.Equal(-20, projectile.X, Tolerance);
        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);

        combat.Step(T + 4);
        Assert.Equal(DummyMaxHp - RangedDamage, Dummy(combat).Hp);
        Assert.Empty(combat.Projectiles);
    }

    [Theory] // AK-7, AK-19
    [InlineData(0, 1)]
    [InlineData(20, 1)]
    [InlineData(21, 2)]
    [InlineData(100, 5)]
    [InlineData(160, 8)]
    public void Projectile_hits_after_ceiling_of_distance_over_step(double distance, int steps)
    {
        var combat = NewCombat();
        var player = NewPlayer(-distance, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        for (var i = 0; i < steps - 1; i++)
        {
            combat.Step(T + (uint)i);
        }

        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);
        Assert.Single(combat.Projectiles);

        combat.Step(T + (uint)steps - 1);
        Assert.Equal(DummyMaxHp - RangedDamage, Dummy(combat).Hp);
        Assert.Empty(combat.Projectiles);
    }

    [Fact] // AK-8, AK-12
    public void Ranged_without_enough_mana_does_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0, mana: ManaCost - 1);

        Assert.Equal(AttackResult.NotEnoughMana, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        AssertUnchanged(combat, player, ManaCost - 1, 0);
    }

    [Fact] // AK-8, AK-10
    public void Ranged_with_exact_mana_cost_works()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0, mana: ManaCost);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        Assert.Equal(0, player.Mana);
        Assert.Single(combat.Projectiles);
    }

    [Fact] // AK-10
    public void Ranged_costs_mana()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        Assert.Equal(100 - ManaCost, player.Mana);
    }

    [Fact] // AK-9, AK-12
    public void Ranged_out_of_range_does_nothing()
    {
        var combat = NewCombat();
        var far = NewPlayer(-RangedRange - 1, 0);

        Assert.Equal(AttackResult.OutOfRange, combat.TryAttack(far, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        AssertUnchanged(combat, far, 100, 0);

        var edge = NewPlayer(-RangedRange, 0, id: "p2");
        Assert.Equal(AttackResult.Ok, combat.TryAttack(edge, Attack.Types.Kind.Ranged, Combat.DummyId, T));
    }

    [Fact] // AK-9
    public void Range_is_checked_only_at_launch()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        player.X = -1000;
        for (var i = 0u; i < 5; i++)
        {
            combat.Step(T + i);
        }

        Assert.Equal(DummyMaxHp - RangedDamage, Dummy(combat).Hp);
    }

    [Fact] // AK-11
    public void Cooldown_blocks_every_attack_kind()
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(T + CooldownTicks, player.NextAttackTick);

        Assert.Equal(AttackResult.Cooldown, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(AttackResult.Cooldown, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T + CooldownTicks - 1));
        Assert.Equal(100, player.Mana);
        Assert.Empty(combat.Projectiles);
        Assert.Equal(DummyMaxHp - MeleeDamage, Dummy(combat).Hp);
        Assert.Equal(T + CooldownTicks, player.NextAttackTick);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T + CooldownTicks));
        Assert.Equal(AttackResult.Cooldown, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T + 2 * CooldownTicks - 1));
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T + 2 * CooldownTicks));
    }

    [Fact] // AK-11
    public void Cooldown_is_per_player()
    {
        var combat = NewCombat();
        var first = NewPlayer(-10, 0);
        var second = NewPlayer(-20, 0, id: "p2");

        Assert.Equal(AttackResult.Ok, combat.TryAttack(first, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(AttackResult.Ok, combat.TryAttack(second, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(DummyMaxHp - 2 * MeleeDamage, Dummy(combat).Hp);
    }

    [Theory] // AK-12
    [InlineData("target")]
    [InlineData("kind")]
    [InlineData("range")]
    [InlineData("facing")]
    [InlineData("mana")]
    public void Failed_attempt_starts_no_cooldown(string cause)
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);
        var id = Combat.DummyId;
        var kind = Attack.Types.Kind.Ranged;
        var expected = AttackResult.Ok;
        switch (cause)
        {
            case "target": id = "fehlt"; expected = AttackResult.InvalidTarget; break;
            case "kind": kind = Attack.Types.Kind.Unspecified; expected = AttackResult.InvalidKind; break;
            case "range": player.X = -200; expected = AttackResult.OutOfRange; break;
            case "facing": player.Facing = Math.PI; expected = AttackResult.NotFacing; break;
            case "mana": player.Mana = 0; expected = AttackResult.NotEnoughMana; break;
        }

        Assert.Equal(expected, combat.TryAttack(player, kind, id, T));
        Assert.Equal(0u, player.NextAttackTick);

        player.X = -10;
        player.Facing = 0;
        player.Mana = 100;
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T + 1));
    }

    [Theory] // AK-19
    [InlineData(Attack.Types.Kind.Melee, 20, 0)]
    [InlineData(Attack.Types.Kind.Melee, 20, 20)]
    [InlineData(Attack.Types.Kind.Melee, 20, -20)]
    [InlineData(Attack.Types.Kind.Ranged, 20, 0)]
    [InlineData(Attack.Types.Kind.Ranged, 20, 20)]
    [InlineData(Attack.Types.Kind.Ranged, 20, -20)]
    public void Target_within_half_arc_counts_as_in_front(Attack.Types.Kind kind, double vx, double vy)
    {
        var combat = NewCombat();
        var player = NewPlayer(-vx, -vy);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, kind, Combat.DummyId, T));
        Assert.Equal(0, player.Facing);
    }

    [Theory] // AK-19, AK-12
    [InlineData(Attack.Types.Kind.Melee, 46)]
    [InlineData(Attack.Types.Kind.Melee, -46)]
    [InlineData(Attack.Types.Kind.Melee, 90)]
    [InlineData(Attack.Types.Kind.Melee, 135)]
    [InlineData(Attack.Types.Kind.Melee, 180)]
    [InlineData(Attack.Types.Kind.Ranged, 46)]
    [InlineData(Attack.Types.Kind.Ranged, -46)]
    [InlineData(Attack.Types.Kind.Ranged, 90)]
    [InlineData(Attack.Types.Kind.Ranged, 135)]
    [InlineData(Attack.Types.Kind.Ranged, 180)]
    public void Target_outside_half_arc_is_rejected(Attack.Types.Kind kind, double degrees)
    {
        var combat = NewCombat();
        var radians = degrees * Math.PI / 180;
        var player = NewPlayer(-20 * Math.Cos(radians), -20 * Math.Sin(radians));

        Assert.Equal(AttackResult.NotFacing, combat.TryAttack(player, kind, Combat.DummyId, T));
        AssertUnchanged(combat, player, 100, 0);
    }

    [Fact] // AK-19
    public void Facing_check_wraps_around_pi()
    {
        var combat = NewCombat();
        var first = NewPlayer(20, 1, facing: Math.PI);
        var second = NewPlayer(20, -1, facing: -Math.PI + 0.01, id: "p2");

        Assert.Equal(AttackResult.Ok, combat.TryAttack(first, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(AttackResult.Ok, combat.TryAttack(second, Attack.Types.Kind.Melee, Combat.DummyId, T));
    }

    [Theory] // AK-19
    [InlineData(Attack.Types.Kind.Melee)]
    [InlineData(Attack.Types.Kind.Ranged)]
    public void Zero_distance_counts_as_in_front(Attack.Types.Kind kind)
    {
        var combat = NewCombat();
        var player = NewPlayer(0, 0, facing: -Math.PI / 2);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, kind, Combat.DummyId, T));
        if (kind == Attack.Types.Kind.Ranged)
        {
            combat.Step(T);
        }

        Assert.Equal(DummyMaxHp - (kind == Attack.Types.Kind.Ranged ? RangedDamage : MeleeDamage), Dummy(combat).Hp);
    }

    [Fact] // AK-2, AK-5, AK-8, AK-11, AK-19
    public void Checks_run_in_plan_order()
    {
        var combat = NewCombat();

        var cooldownUnknown = NewPlayer(-10, 0);
        cooldownUnknown.NextAttackTick = T + 5;
        Assert.Equal(AttackResult.InvalidTarget, combat.TryAttack(cooldownUnknown, Attack.Types.Kind.Melee, "fehlt", T));

        var cooldownFar = NewPlayer(-200, 0, id: "p2");
        cooldownFar.NextAttackTick = T + 5;
        Assert.Equal(AttackResult.Cooldown, combat.TryAttack(cooldownFar, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(T + 5, cooldownFar.NextAttackTick);

        var farBehind = NewPlayer(-200, 0, facing: Math.PI, id: "p3");
        Assert.Equal(AttackResult.OutOfRange, combat.TryAttack(farBehind, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        var behindNoMana = NewPlayer(-100, 0, facing: Math.PI, mana: 0, id: "p4");
        Assert.Equal(AttackResult.NotFacing, combat.TryAttack(behindNoMana, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        var unspecified = NewPlayer(-10, 0, id: "p5");
        Assert.Equal(AttackResult.InvalidKind, combat.TryAttack(unspecified, Attack.Types.Kind.Unspecified, "fehlt", T));

        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);
        Assert.Empty(combat.Projectiles);
        Assert.Equal(0u, farBehind.NextAttackTick);
        Assert.Equal(0u, behindNoMana.NextAttackTick);
        Assert.Equal(0u, unspecified.NextAttackTick);
    }

    [Fact] // AK-19
    public void Turning_towards_the_target_allows_the_attack()
    {
        var world = new World(100, TickRate, new Random(1));
        var player = world.AddPlayer("p1", "Test", 0, -20, 0);
        var combat = NewCombat();

        Assert.Equal(AttackResult.NotFacing, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, 1));
        Assert.Equal(Math.PI / 2, player.Facing);

        world.EnqueueInput("p1", new MoveInput(1, 1, 0));
        world.Step();

        Assert.Equal(0, player.Facing);
        Assert.Equal(-15, player.X, Tolerance);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, 2));
        Assert.Equal(DummyMaxHp - MeleeDamage, Dummy(combat).Hp);
    }

    [Fact] // AK-7, AK-14
    public void Projectile_on_dead_target_vanishes()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        combat.Step(T);
        Combat.Damage(Dummy(combat), DummyMaxHp);
        combat.Step(T + 1);

        Assert.Empty(combat.Projectiles);
        Assert.Equal(0, Dummy(combat).Hp);

        for (var tick = T + 2; tick <= T + 1 + combat.DummyRespawnTicks; tick++)
        {
            combat.Step(tick);
        }

        Assert.Equal(DummyMaxHp, Dummy(combat).Hp);
    }

    [Fact] // AK-7, AK-14
    public void Killing_impact_lets_later_projectile_vanish()
    {
        var combat = NewCombat(dummyMaxHp: RangedDamage);
        var first = NewPlayer(-100, 0);
        var second = NewPlayer(-100, 0, id: "p2");

        Assert.Equal(AttackResult.Ok, combat.TryAttack(first, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        Assert.Equal(AttackResult.Ok, combat.TryAttack(second, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        for (var i = 0u; i < 5; i++)
        {
            combat.Step(T + i);
        }

        Assert.Equal(0, Dummy(combat).Hp);
        Assert.Empty(combat.Projectiles);
        Assert.Equal(T + 4 + combat.DummyRespawnTicks, Dummy(combat).RespawnAtTick);
    }

    [Fact] // AK-2, AK-14
    public void Melee_kill_makes_target_invalid_in_the_same_tick()
    {
        var combat = NewCombat(dummyMaxHp: MeleeDamage);
        var first = NewPlayer(-10, 0);
        var second = NewPlayer(-10, 0, id: "p2");

        Assert.Equal(AttackResult.Ok, combat.TryAttack(first, Attack.Types.Kind.Melee, Combat.DummyId, T));
        Assert.Equal(0, Dummy(combat).Hp);
        Assert.Equal(AttackResult.InvalidTarget, combat.TryAttack(second, Attack.Types.Kind.Melee, Combat.DummyId, T));
    }

    [Fact] // AK-7
    public void Projectile_ids_are_unique_and_never_zero()
    {
        var combat = NewCombat();
        for (var i = 0; i < 3; i++)
        {
            var player = NewPlayer(-100, 0, id: $"p{i}");
            Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        }

        var ids = combat.Projectiles.Select(p => p.Id).ToArray();
        Assert.Equal(3, ids.Length);
        Assert.DoesNotContain(0u, ids);
        Assert.Equal(ids.Order().Distinct(), ids);
    }

    [Fact] // AK-7
    public void Default_projectile_step_matches_the_plan()
    {
        Assert.Equal(GameSettings.ProjectileSpeed / GameSettings.TickRateHz, new Combat(GameSettings.TickRateHz).ProjectileStepDistance);
    }
}
