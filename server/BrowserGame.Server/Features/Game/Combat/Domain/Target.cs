using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>Zielbares Objekt mit HP (013: Trainings-Dummy; 014: Gegner). Steht still. Lebt nur im Speicher (013 AK-17).</summary>
internal sealed class Target(string id, TargetState.Types.Kind kind, double x, double y, double maxHp)
{
    public string Id { get; } = id;
    public TargetState.Types.Kind Kind { get; } = kind;
    public double X { get; } = x;
    public double Y { get; } = y;
    public double MaxHp { get; } = maxHp;

    /// <summary>Aktuelle HP, zwischen 0 und <see cref="MaxHp"/>.</summary>
    public double Hp { get; internal set; } = maxHp;

    /// <summary>Tick, ab dem das tote Ziel mit vollen HP zurückkehrt (013 AK-15); <c>null</c>, solange es nicht wartet.</summary>
    public uint? RespawnAtTick { get; internal set; }

    public bool IsAlive => RespawnAtTick is null && Hp > 0;
}
