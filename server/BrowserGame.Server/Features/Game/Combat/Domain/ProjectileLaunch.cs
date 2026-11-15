namespace BrowserGame.Server.Features.Game;

/// <summary>Ein Fernkampf-Projektil ist gestartet (013 AK-21); der Client spielt dafür einen Effekt ab.</summary>
internal readonly record struct ProjectileLaunch(uint ProjectileId, string ShooterId, string TargetId);
