namespace BrowserGame.Server.Features.Game;

/// <summary>Fliegendes Fernkampf-Projektil; verfolgt sein Ziel und schlägt dort ein (013 AK-7).</summary>
internal sealed class Projectile(uint id, double x, double y, string targetId, double damage)
{
    public uint Id { get; } = id;
    public double X { get; internal set; } = x;
    public double Y { get; internal set; } = y;
    public string TargetId { get; } = targetId;
    public double Damage { get; } = damage;
}
