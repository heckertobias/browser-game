using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;

namespace BrowserGame.Server.Tests;

file static class EventSockets
{
    public const float FacingUp = (float)(-Math.PI / 2);

    // Fester Start vor dem Verbinden; nach dem Drehen nach oben sind es 97 bis zum Dummy.
    public static readonly (double X, double Y) RangedSpot = (GameSettings.DummyX, GameSettings.DummyY + 100);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<(WebSocket Socket, string Id, Snapshot First)> JoinAtAsync(
        GameServerFactory server, (double X, double Y) position)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(server);
        var id = await GameSockets.AccountIdAsync(server, account.Email);
        await GameSockets.SeedPositionAsync(server, id, position.X, position.Y);
        var socket = await GameSockets.ConnectAsync(server, await GameSockets.SignInCookieAsync(server, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        var first = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return (socket, id, first);
    }

    public static Task SendAttackAsync(WebSocket socket, Attack.Types.Kind kind) =>
        socket.SendAsync(
            new ClientMessage { Attack = new Attack { Kind = kind, TargetId = Combat.DummyId } }.ToByteArray(),
            WebSocketMessageType.Binary, true, Ct);

    public static async Task FaceDummyAsync(WebSocket socket, string id)
    {
        await GameSockets.SendInputAsync(socket, 1, 0, -0.1f, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id && p.Facing == FacingUp), Ct);
    }

    public static TargetState Dummy(Snapshot snapshot) => snapshot.Targets.Single(t => t.Id == Combat.DummyId);
}

/// <summary>
/// Start und Treffer eines Fernkampfs erreichen Angreifer und Zuschauer über echte WebSockets in dieser Reihenfolge
/// (013 AK-7, AK-21).
/// </summary>
public sealed class GameCombatEventsSocketTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-7, AK-21
    public async Task Ranged_attack_reaches_attacker_and_observer_as_launch_then_hit()
    {
        await using var server = new GameServerFactory();
        var (observer, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        using var observerSocket = observer;
        var welcome = await GameSockets.ReceiveWelcomeAsync(observer, Ct);
        var (socket, id, _) = await EventSockets.JoinAtAsync(server, EventSockets.RangedSpot);
        using var _ = socket;
        await EventSockets.FaceDummyAsync(socket, id);

        await EventSockets.SendAttackAsync(socket, Attack.Types.Kind.Ranged);
        var started = await GameSockets.ReceiveUntilAsync(
            socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.CombatEvents && m.CombatEvents.Launched.Count > 0, Ct);
        var launch = Assert.Single(started.CombatEvents.Launched);
        var lastHp = float.NaN;
        var landed = await GameSockets.ReceiveUntilAsync(socket, m =>
        {
            if (m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot && m.Snapshot.Targets.Count > 0)
            {
                lastHp = EventSockets.Dummy(m.Snapshot).Hp;
            }

            return m.PayloadCase == ServerMessage.PayloadOneofCase.CombatEvents
                && m.CombatEvents.Hits.Any(h => h.ProjectileId == launch.ProjectileId);
        }, Ct);
        var hit = landed.CombatEvents.Hits.Single(h => h.ProjectileId == launch.ProjectileId);
        var next = await GameSockets.ReceiveSnapshotUntilAsync(socket, _ => true, Ct);

        Assert.Equal((float)GameSettings.ProjectileSpeed, welcome.ProjectileSpeed);
        Assert.Equal(id, launch.ShooterId);
        Assert.Equal(Combat.DummyId, launch.TargetId);
        Assert.Equal((float)GameSettings.RangedDamage, hit.Damage);
        Assert.Equal(lastHp - hit.Damage, EventSockets.Dummy(next).Hp);

        var seenLaunch = false;
        var seenHit = false;
        await GameSockets.ReceiveUntilAsync(observer, m =>
        {
            if (m.PayloadCase == ServerMessage.PayloadOneofCase.CombatEvents)
            {
                seenLaunch |= m.CombatEvents.Launched.Any(l => l.ProjectileId == launch.ProjectileId);
                seenHit |= m.CombatEvents.Hits.Any(h => h.ProjectileId == launch.ProjectileId);
            }

            return seenLaunch && seenHit;
        }, Ct);
    }
}
