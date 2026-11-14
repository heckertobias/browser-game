using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Kampfregeln ohne Netzwerk und Datenbank (013). Wird ausschließlich vom Tick-Thread benutzt.
/// Hält genau einen Trainings-Dummy; er lebt nur im Speicher und entsteht mit jeder Instanz neu (013 AK-13, AK-17).
/// Der Dummy hat keinerlei Angriffslogik (013 AK-16).
/// Prüft und führt Angriffe aus und lässt Projektile fliegen (013 AK-4–12, AK-19).
/// </summary>
internal sealed class Combat(
    int tickRateHz,
    double attackArcDegrees = Combat.DefaultAttackArcDegrees,
    double meleeRange = Combat.DefaultMeleeRange,
    double meleeDamage = Combat.DefaultMeleeDamage,
    double rangedRange = Combat.DefaultRangedRange,
    double rangedDamage = Combat.DefaultRangedDamage,
    double rangedManaCost = Combat.DefaultRangedManaCost,
    double projectileSpeed = Combat.DefaultProjectileSpeed,
    double globalCooldownSeconds = Combat.DefaultGlobalCooldownSeconds,
    double dummyX = Combat.DefaultDummyX,
    double dummyY = Combat.DefaultDummyY,
    double dummyMaxHp = Combat.DefaultDummyMaxHp,
    double dummyRespawnSeconds = Combat.DefaultDummyRespawnSeconds)
{
    public const string DummyId = "dummy";

    /// <summary>Öffnungswinkel nach vorn in Grad (± die Hälfte um die Blickrichtung), für beide Angriffe (013 AK-19).</summary>
    public const double DefaultAttackArcDegrees = 90;

    /// <summary>Nahkampfreichweite, Mittelpunkt zu Mittelpunkt in Welteinheiten (013 AK-4, AK-5).</summary>
    public const double DefaultMeleeRange = 32;

    /// <summary>Nahkampfschaden (013 AK-4).</summary>
    public const double DefaultMeleeDamage = 10;

    /// <summary>Maximalreichweite des Fernkampfs, geprüft beim Start (013 AK-9).</summary>
    public const double DefaultRangedRange = 160;

    /// <summary>Fernkampfschaden beim Einschlag (013 AK-7).</summary>
    public const double DefaultRangedDamage = 15;

    /// <summary>Mana je Fernkampf (013 AK-10).</summary>
    public const double DefaultRangedManaCost = 20;

    /// <summary>Projektil-Tempo in Welteinheiten pro Sekunde (013 AK-7).</summary>
    public const double DefaultProjectileSpeed = 400;

    /// <summary>Allgemeiner Cooldown nach einem erfolgreichen Angriff in Sekunden (013 AK-11).</summary>
    public const double DefaultGlobalCooldownSeconds = 1;

    /// <summary>Feste Position des Trainings-Dummys, x (013 AK-13).</summary>
    public const double DefaultDummyX = 0;

    /// <summary>Feste Position des Trainings-Dummys, y; außerhalb des Spawn-Radius (013 AK-13).</summary>
    public const double DefaultDummyY = -128;

    /// <summary>Start-HP des Trainings-Dummys (013 AK-13).</summary>
    public const double DefaultDummyMaxHp = 100;

    /// <summary>Verzögerung in Sekunden, bis der Dummy mit vollen HP zurückkehrt (013 AK-15).</summary>
    public const double DefaultDummyRespawnSeconds = 5;

    // Rundungstoleranz für den Winkelvergleich, kein Spielwert: „genau auf der Grenze“ zählt als vorn (plan.md Entscheidung 10).
    private const double AngleTolerance = 1e-9;

    private readonly Dictionary<string, Target> _targets = new()
    {
        [DummyId] = new Target(DummyId, TargetState.Types.Kind.TrainingDummy, dummyX, dummyY, dummyMaxHp),
    };

    private readonly List<Projectile> _projectiles = [];
    private uint _nextProjectileId = 1;

    public int TickRateHz { get; } = tickRateHz;
    public double AttackArcDegrees { get; } = attackArcDegrees;
    public double MeleeRange { get; } = meleeRange;
    public double MeleeDamage { get; } = meleeDamage;
    public double RangedRange { get; } = rangedRange;
    public double RangedDamage { get; } = rangedDamage;
    public double RangedManaCost { get; } = rangedManaCost;
    public double ProjectileSpeed { get; } = projectileSpeed;
    public uint GlobalCooldownTicks { get; } = ToTicks(globalCooldownSeconds, tickRateHz);
    public uint DummyRespawnTicks { get; } = ToTicks(dummyRespawnSeconds, tickRateHz);

    /// <summary>Strecke eines Projektils pro Tick.</summary>
    public double ProjectileStepDistance => ProjectileSpeed / TickRateHz;

    /// <summary>Fliegende Projektile in Startreihenfolge.</summary>
    public IReadOnlyCollection<Projectile> Projectiles => _projectiles;

    /// <summary>Alle Ziele, auch tote.</summary>
    public IReadOnlyCollection<Target> Targets => _targets.Values;

    /// <summary>Lebende Ziele; nur sie gehen in den Snapshot.</summary>
    public IEnumerable<Target> AliveTargets => _targets.Values.Where(t => t.IsAlive);

    public Target? GetTarget(string id) => _targets.GetValueOrDefault(id);

    /// <summary>Senkt die HP, nie unter 0 (013 AK-14). Nicht positive Werte und NaN werden ignoriert.</summary>
    public static void Damage(Target target, double amount)
    {
        if (!(amount > 0))
        {
            return;
        }

        target.Hp = Math.Max(0, target.Hp - amount);
    }

    /// <summary>
    /// Angriffsversuch im Tick <paramref name="tick"/>. Prüft Art → Ziel → Cooldown → Reichweite → Blickrichtung → Mana
    /// (plan.md Entscheidung 3). Nur bei <see cref="AttackResult.Ok"/> ändert sich etwas: Nahkampf schadet sofort (AK-4, AK-6),
    /// Fernkampf kostet Mana und startet ein Projektil (AK-7, AK-10); der Cooldown beginnt (AK-11). Die Figur dreht sich nie (AK-19).
    /// </summary>
    public AttackResult TryAttack(Player attacker, Attack.Types.Kind kind, string targetId, uint tick)
    {
        if (kind is not (Attack.Types.Kind.Melee or Attack.Types.Kind.Ranged))
        {
            return AttackResult.InvalidKind;
        }

        if (GetTarget(targetId) is not { IsAlive: true } target)
        {
            return AttackResult.InvalidTarget;
        }

        if (tick < attacker.NextAttackTick)
        {
            return AttackResult.Cooldown;
        }

        var dx = target.X - attacker.X;
        var dy = target.Y - attacker.Y;
        var melee = kind == Attack.Types.Kind.Melee;
        if (!(Math.Sqrt(dx * dx + dy * dy) <= (melee ? MeleeRange : RangedRange)))
        {
            return AttackResult.OutOfRange;
        }

        if (!IsInFront(attacker.Facing, dx, dy))
        {
            return AttackResult.NotFacing;
        }

        if (melee)
        {
            Damage(target, MeleeDamage);
        }
        else
        {
            if (attacker.Mana < RangedManaCost)
            {
                return AttackResult.NotEnoughMana;
            }

            attacker.Mana -= RangedManaCost;
            _projectiles.Add(new Projectile(_nextProjectileId++, attacker.X, attacker.Y, target.Id, RangedDamage));
        }

        attacker.NextAttackTick = tick + GlobalCooldownTicks;
        return AttackResult.Ok;
    }

    /// <summary>
    /// Ein Tick: Zuerst fliegen Projektile und schlagen ein (013 AK-7, AK-14); Projektile auf ein totes oder
    /// fehlendes Ziel verfallen ohne Wirkung. Dann: Ziele mit 0 HP warten <see cref="DummyRespawnTicks"/> Ticks und kehren dann mit vollen HP zurück (013 AK-15).
    /// </summary>
    public void Step(uint tick)
    {
        MoveProjectiles();

        foreach (var target in _targets.Values)
        {
            if (target.RespawnAtTick is null && target.Hp <= 0)
            {
                target.RespawnAtTick = tick + DummyRespawnTicks;
            }

            if (target.RespawnAtTick is { } respawnAt && tick >= respawnAt)
            {
                target.Hp = target.MaxHp;
                target.RespawnAtTick = null;
            }
        }
    }

    /// <summary>
    /// Liegt der Vektor (<paramref name="dx"/>, <paramref name="dy"/>) höchstens <see cref="AttackArcDegrees"/>/2 neben
    /// <paramref name="facing"/>? Abstand 0 zählt als vorn (plan.md Entscheidung 10).
    /// </summary>
    private bool IsInFront(double facing, double dx, double dy)
    {
        if (dx == 0 && dy == 0)
        {
            return true;
        }

        var diff = Math.Abs(Math.IEEERemainder(Math.Atan2(dy, dx) - facing, 2 * Math.PI));
        return diff <= AttackArcDegrees / 2 * Math.PI / 180 + AngleTolerance;
    }

    /// <summary>Bewegt alle Projektile um <see cref="ProjectileStepDistance"/> auf ihr Ziel zu; ist der Rest nicht länger, schlagen sie ein.</summary>
    private void MoveProjectiles()
    {
        var step = ProjectileStepDistance;
        var i = 0;
        while (i < _projectiles.Count)
        {
            var projectile = _projectiles[i];
            if (GetTarget(projectile.TargetId) is not { IsAlive: true } target)
            {
                _projectiles.RemoveAt(i);
                continue;
            }

            var dx = target.X - projectile.X;
            var dy = target.Y - projectile.Y;
            var rest = Math.Sqrt(dx * dx + dy * dy);
            if (rest <= step)
            {
                Damage(target, projectile.Damage);
                _projectiles.RemoveAt(i);
                continue;
            }

            projectile.X += dx / rest * step;
            projectile.Y += dy / rest * step;
            i++;
        }
    }

    /// <summary>Sekunden in ganze Ticks, aufgerundet; negative Werte zählen als 0 (wie <c>GameSettings.ToTicks</c>).</summary>
    private static uint ToTicks(double seconds, int tickRateHz) => (uint)Math.Ceiling(Math.Max(0, seconds) * tickRateHz);
}
