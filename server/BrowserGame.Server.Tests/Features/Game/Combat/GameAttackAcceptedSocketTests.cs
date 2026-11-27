using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;

namespace BrowserGame.Server.Tests;

file static class AcceptedSockets
{
    public const float FacingUp = (float)(-Math.PI / 2);

    // Fester Start vor dem Verbinden; nach dem Drehen nach oben sind es 25 bis zum Dummy.
    public static readonly (double X, double Y) MeleeSpot = (GameSettings.DummyX, GameSettings.DummyY + 28);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<(WebSocket Socket, string Id)> JoinAtAsync(GameServerFactory server, (double X, double Y) position)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(server);
        var id = await GameSockets.AccountIdAsync(server, account.Email);
        await GameSockets.SeedPositionAsync(server, id, position.X, position.Y);
        var socket = await GameSockets.ConnectAsync(server, await GameSockets.SignInCookieAsync(server, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return (socket, id);
    }

    public static Task SendAttackAsync(WebSocket socket, Attack.Types.Kind kind) =>
        socket.SendAsync(
            new ClientMessage { Attack = new Attack { Kind = kind, TargetId = Combat.DummyId } }.ToByteArray(),
            WebSocketMessageType.Binary, true, Ct);

    // Eingabe nach oben; wartet, bis der Server die Figur zum Dummy dreht.
    public static async Task FaceDummyAsync(WebSocket socket, string id)
    {
        await GameSockets.SendInputAsync(socket, 1, 0, -0.1f, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id && p.Facing == FacingUp), Ct);
    }
}

/// <summary>Bestätigung eines Angriffs mit Abklingzeit über echte WebSockets (013 AK-22).</summary>
public sealed class GameAttackAcceptedSocketTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-22
    public async Task Melee_over_socket_is_confirmed_with_the_cooldown()
    {
        await using var server = new GameServerFactory();
        var (socket, id) = await AcceptedSockets.JoinAtAsync(server, AcceptedSockets.MeleeSpot);
        using var _ = socket;
        await AcceptedSockets.FaceDummyAsync(socket, id);

        await AcceptedSockets.SendAttackAsync(socket, Attack.Types.Kind.Melee);
        var rejected = false;
        var accepted = await GameSockets.ReceiveUntilAsync(socket, m =>
        {
            rejected |= m.PayloadCase == ServerMessage.PayloadOneofCase.AttackRejected;
            return m.PayloadCase == ServerMessage.PayloadOneofCase.AttackAccepted;
        }, Ct);

        Assert.False(rejected);
        Assert.Equal(
            (float)((double)GameSettings.ToTicks(GameSettings.GlobalCooldownSeconds) / GameSettings.TickRateHz),
            accepted.AttackAccepted.CooldownSeconds);
    }
}
