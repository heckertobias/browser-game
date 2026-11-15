namespace BrowserGame.Server.Features.Game;

/// <summary>Ein Projektil hat eingeschlagen (013 AK-7, AK-21); <see cref="Damage"/> sind die tatsächlich abgezogenen HP.</summary>
internal readonly record struct ProjectileImpact(uint ProjectileId, string TargetId, double Damage);
