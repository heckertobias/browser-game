using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Kampfregeln ohne Netzwerk und Datenbank (013). Wird ausschließlich vom Tick-Thread benutzt.
/// Hält genau einen Trainings-Dummy; er lebt nur im Speicher und entsteht mit jeder Instanz neu (013 AK-13, AK-17).
/// Der Dummy hat keinerlei Angriffslogik (013 AK-16).
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

    private readonly Dictionary<string, Target> _targets = new()
    {
        [DummyId] = new Target(DummyId, TargetState.Types.Kind.TrainingDummy, dummyX, dummyY, dummyMaxHp),
    };

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
    /// Ein Tick: Ziele mit 0 HP warten <see cref="DummyRespawnTicks"/> Ticks und kehren dann mit vollen HP zurück (013 AK-15).
    /// </summary>
    public void Step(uint tick)
    {
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

    /// <summary>Sekunden in ganze Ticks, aufgerundet; negative Werte zählen als 0 (wie <c>GameSettings.ToTicks</c>).</summary>
    private static uint ToTicks(double seconds, int tickRateHz) => (uint)Math.Ceiling(Math.Max(0, seconds) * tickRateHz);
}
