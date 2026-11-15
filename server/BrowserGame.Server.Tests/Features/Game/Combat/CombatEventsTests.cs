using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CombatEventsTests
{
    private const int TickRate = 20;
    private const double MeleeRange = 32;
    private const double MeleeDamage = 10;
    private const double RangedRange = 160;
    private const double RangedDamage = 15;
    private const double ManaCost = 20;
    private const double ProjectileSpeed = 400;
    private const double DummyMaxHp = 100;
    private const uint T = 1000;

    private static Combat NewCombat() => new(
        TickRate, attackArcDegrees: 90, meleeRange: MeleeRange, meleeDamage: MeleeDamage, rangedRange: RangedRange,
        rangedDamage: RangedDamage, rangedManaCost: ManaCost, projectileSpeed: ProjectileSpeed, globalCooldownSeconds: 1,
        dummyX: 0, dummyY: 0, dummyMaxHp: DummyMaxHp, dummyRespawnSeconds: 5);

    private static Player NewPlayer(double x, double y, double facing = 0) =>
        new("p1", "Test", 0, x, y) { Hp = 100, Mana = 100, Facing = facing };

    private static Target Dummy(Combat combat) => combat.GetTarget(Combat.DummyId)!;

    private static void AssertNothingReported(Combat combat)
    {
        Assert.Empty(combat.Launches);
        Assert.Empty(combat.Impacts);
    }

    [Fact] // AK-7, AK-21
    public void Ranged_ok_reports_a_launch_and_no_impact()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        var launch = Assert.Single(combat.Launches);
        Assert.Equal(Assert.Single(combat.Projectiles).Id, launch.ProjectileId);
        Assert.Equal("p1", launch.ShooterId);
        Assert.Equal(Combat.DummyId, launch.TargetId);
        Assert.Empty(combat.Impacts);
    }

    [Fact] // AK-7, AK-14, AK-21
    public void Impact_is_reported_in_the_step_that_lowers_hp()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        var id = Assert.Single(combat.Launches).ProjectileId;

        for (var i = 0u; i < 4; i++)
        {
            combat.Step(T + i);
            Assert.Empty(combat.Impacts);
            Assert.Equal(DummyMaxHp, Dummy(combat).Hp);
        }

        combat.Step(T + 4);

        var impact = Assert.Single(combat.Impacts);
        Assert.Equal(id, impact.ProjectileId);
        Assert.Equal(Combat.DummyId, impact.TargetId);
        Assert.Equal(RangedDamage, impact.Damage);
        Assert.Equal(DummyMaxHp - RangedDamage, Dummy(combat).Hp);
    }

    [Fact] // AK-21
    public void Impact_reports_only_the_hp_actually_lost()
    {
        var combat = NewCombat();
        Combat.Damage(Dummy(combat), 95);
        var player = NewPlayer(-10, 0);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        combat.Step(T);

        Assert.Equal(5, Assert.Single(combat.Impacts).Damage);
        Assert.Equal(0, Dummy(combat).Hp);
    }

    [Fact] // AK-15, AK-21
    public void Projectile_on_dead_target_reports_no_impact()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        Combat.Damage(Dummy(combat), DummyMaxHp);

        combat.Step(T);

        Assert.Empty(combat.Impacts);
        Assert.Empty(combat.Projectiles);
    }

    [Fact] // AK-6, AK-21
    public void Melee_reports_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Melee, Combat.DummyId, T));

        Assert.Empty(combat.Launches);
        Assert.Empty(combat.Impacts);
    }

    [Fact] // AK-8, AK-21
    public void Ranged_without_enough_mana_reports_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);
        player.Mana = ManaCost - 1;

        Assert.Equal(AttackResult.NotEnoughMana, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        AssertNothingReported(combat);
    }

    [Fact] // AK-9, AK-21
    public void Ranged_out_of_range_reports_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-161, 0);

        Assert.Equal(AttackResult.OutOfRange, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        AssertNothingReported(combat);
    }

    [Fact] // AK-19, AK-21
    public void Ranged_not_facing_reports_nothing()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0, facing: Math.PI);

        Assert.Equal(AttackResult.NotFacing, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        AssertNothingReported(combat);
    }

    [Fact] // AK-11, AK-21
    public void Ranged_during_cooldown_reports_only_the_first_launch()
    {
        var combat = NewCombat();
        var player = NewPlayer(-100, 0);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        Assert.Equal(AttackResult.Cooldown, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));

        Assert.Single(combat.Launches);
        Assert.Empty(combat.Impacts);
    }

    [Fact] // AK-21
    public void ClearEvents_forgets_launches_and_impacts()
    {
        var combat = NewCombat();
        var player = NewPlayer(-10, 0);
        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T));
        combat.Step(T);
        Assert.Single(combat.Impacts);

        combat.ClearEvents();

        Assert.Empty(combat.Launches);
        Assert.Empty(combat.Impacts);

        Assert.Equal(AttackResult.Ok, combat.TryAttack(player, Attack.Types.Kind.Ranged, Combat.DummyId, T + 20));
        combat.Step(T + 20);

        Assert.Single(combat.Launches);
        Assert.Single(combat.Impacts);
    }

    [Theory] // AK-14
    [InlineData(100, 20, 20, 80)]
    [InlineData(5, 15, 5, 0)]
    [InlineData(100, 0, 0, 100)]
    [InlineData(100, -1, 0, 100)]
    [InlineData(100, double.NaN, 0, 100)]
    public void Damage_returns_the_hp_actually_lost(double hp, double amount, double lost, double expectedHp)
    {
        var combat = NewCombat();
        var target = Dummy(combat);
        target.Hp = hp;

        Assert.Equal(lost, Combat.Damage(target, amount));
        Assert.Equal(expectedHp, target.Hp);
    }
}
