using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;

namespace BrowserGame.Server.Tests;

file static class CombatSockets
{
    public const float FacingUp = (float)(-Math.PI / 2);

    // Fester Start vor dem Verbinden; nach dem Drehen nach oben sind es 25 (Nahkampf) bzw. 97 (Fernkampf) bis zum Dummy.
    public static readonly (double X, double Y) MeleeSpot = (GameSettings.DummyX, GameSettings.DummyY + 28);
    public static readonly (double X, double Y) RangedSpot = (GameSettings.DummyX, GameSettings.DummyY + 100);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<(WebSocket Socket, string Id, string Email, Snapshot First)> JoinAtAsync(
        GameServerFactory server, (double X, double Y) position)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(server);
        var id = await GameSockets.AccountIdAsync(server, account.Email);
        await GameSockets.SeedPositionAsync(server, id, position.X, position.Y);
        var socket = await GameSockets.ConnectAsync(server, await GameSockets.SignInCookieAsync(server, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        var first = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return (socket, id, account.Email, first);
    }

    public static async Task<WebSocket> ObserverAsync(GameServerFactory server)
    {
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        return socket;
    }

    public static Task SendAttackAsync(WebSocket socket, Attack.Types.Kind kind, string targetId = Combat.DummyId) =>
        socket.SendAsync(
            new ClientMessage { Attack = new Attack { Kind = kind, TargetId = targetId } }.ToByteArray(),
            WebSocketMessageType.Binary, true, Ct);

    // Eingabe nach oben; wartet, bis der Server die Figur zum Dummy dreht.
    public static async Task FaceDummyAsync(WebSocket socket, string id)
    {
        await GameSockets.SendInputAsync(socket, 1, 0, -0.1f, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id && p.Facing == FacingUp), Ct);
    }

    public static TargetState Dummy(Snapshot snapshot) => snapshot.Targets.Single(t => t.Id == Combat.DummyId);

    // Pflichtnachrichten fallen nie weg und gehen vor jedem späteren Snapshot raus; so ist "keine Ablehnung" unabhängig vom Lese-Tempo.
    public static async Task<bool> SawRejectionUntilAsync(WebSocket socket, uint tick)
    {
        var saw = false;
        await GameSockets.ReceiveUntilAsync(socket, m =>
        {
            saw |= m.PayloadCase == ServerMessage.PayloadOneofCase.AttackRejected;
            return m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot && m.Snapshot.Tick >= tick;
        }, Ct);
        return saw;
    }
}

public sealed class GameCombatSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const float MaxMana = (float)GameSettings.MaxMana;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GameLoop Game(GameServerFactory server) => server.Services.GetRequiredService<GameLoop>();

    [Fact] // AK-7
    public async Task Ranged_attack_over_socket_costs_mana_and_lowers_dummy_hp()
    {
        var (socket, id, _, first) = await CombatSockets.JoinAtAsync(factory, CombatSockets.RangedSpot);
        using var _ = socket;
        var before = CombatSockets.Dummy(first).Hp;
        await CombatSockets.FaceDummyAsync(socket, id);

        await CombatSockets.SendAttackAsync(socket, Attack.Types.Kind.Ranged);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Mana < MaxMana, Ct);
        var hit = await GameSockets.ReceiveSnapshotUntilAsync(
            socket, s => CombatSockets.Dummy(s).Hp == before - (float)GameSettings.RangedDamage, Ct);

        Assert.Empty(hit.Projectiles);
    }

    [Fact] // AK-8
    public async Task Not_enough_mana_over_socket_reaches_only_the_attacker()
    {
        using var observer = await CombatSockets.ObserverAsync(factory);
        var (socket, id, _, first) = await CombatSockets.JoinAtAsync(factory, CombatSockets.RangedSpot);
        using var _ = socket;
        var before = CombatSockets.Dummy(first).Hp;
        await CombatSockets.FaceDummyAsync(socket, id);

        Game(factory).SetVitals(id, mana: 0);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Mana < (float)GameSettings.RangedManaCost, Ct);
        await CombatSockets.SendAttackAsync(socket, Attack.Types.Kind.Ranged);
        var rejected = await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.AttackRejected, Ct);
        var next = await GameSockets.ReceiveSnapshotUntilAsync(socket, _ => true, Ct);

        Assert.Equal(AttackRejected.Types.Reason.NotEnoughMana, rejected.AttackRejected.Reason);
        Assert.Empty(next.Projectiles);
        Assert.Equal(before, CombatSockets.Dummy(next).Hp);
        Assert.False(await CombatSockets.SawRejectionUntilAsync(observer, next.Tick + 1));
    }

    [Fact] // AK-19
    public async Task Not_facing_over_socket_reaches_only_the_attacker()
    {
        using var observer = await CombatSockets.ObserverAsync(factory);
        var (socket, id, _, first) = await CombatSockets.JoinAtAsync(factory, CombatSockets.MeleeSpot);
        using var _ = socket;
        var before = CombatSockets.Dummy(first).Hp;

        await CombatSockets.SendAttackAsync(socket, Attack.Types.Kind.Melee);
        var rejected = await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.AttackRejected, Ct);
        var next = await GameSockets.ReceiveSnapshotUntilAsync(socket, _ => true, Ct);

        Assert.Equal(AttackRejected.Types.Reason.NotFacing, rejected.AttackRejected.Reason);
        Assert.Equal(before, CombatSockets.Dummy(next).Hp);
        Assert.Equal((float)GameSettings.DefaultFacing, next.Players.Single(p => p.Id == id).Facing);
        Assert.False(await CombatSockets.SawRejectionUntilAsync(observer, next.Tick + 1));
    }
}

public sealed class GameCombatServerTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-13
    public async Task Fresh_server_shows_the_dummy_in_the_first_snapshot()
    {
        await using var server = new GameServerFactory();
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        using var _ = socket;
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);

        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, _ => true, Ct);

        var dummy = Assert.Single(snapshot.Targets);
        Assert.Equal(Combat.DummyId, dummy.Id);
        Assert.Equal(TargetState.Types.Kind.TrainingDummy, dummy.Kind);
        Assert.Equal((float)GameSettings.DummyX, dummy.X);
        Assert.Equal((float)GameSettings.DummyY, dummy.Y);
        Assert.Equal((float)GameSettings.DummyMaxHp, dummy.Hp);
        Assert.Equal((float)GameSettings.DummyMaxHp, dummy.MaxHp);
        Assert.Empty(snapshot.Projectiles);
    }

    [Fact] // AK-17
    public async Task Restarted_server_brings_a_fresh_dummy()
    {
        await using var first = new GameServerFactory { KeepDatabase = true };
        await using var second = new GameServerFactory { ConnectionString = first.ConnectionString };
        var (socket, id, email, _) = await CombatSockets.JoinAtAsync(first, CombatSockets.MeleeSpot);
        using var _ = socket;
        await CombatSockets.FaceDummyAsync(socket, id);
        await CombatSockets.SendAttackAsync(socket, Attack.Types.Kind.Melee);
        await GameSockets.ReceiveSnapshotUntilAsync(
            socket, s => CombatSockets.Dummy(s).Hp == (float)(GameSettings.DummyMaxHp - GameSettings.MeleeDamage), Ct);

        await first.DisposeAsync();

        using var again = await GameSockets.ConnectAsync(second, await GameSockets.SignInCookieAsync(second, email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(again, Ct);
        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(again, _ => true, Ct);

        Assert.Equal((float)GameSettings.DummyMaxHp, CombatSockets.Dummy(snapshot).Hp);
    }
}
