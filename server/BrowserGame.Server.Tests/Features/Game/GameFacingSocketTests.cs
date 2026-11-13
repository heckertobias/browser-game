using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class GameFacingSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const float DefaultFacing = (float)GameSettings.DefaultFacing;
    private const float FacingLeft = (float)Math.PI;
    private const float FacingUp = (float)(-Math.PI / 2);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<(WebSocket Socket, string Id, string Email)> JoinAsync(GameServerFactory server)
    {
        var (socket, id, account) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return (socket, id, account.Email);
    }

    private static async Task<WebSocket> ObserverAsync(GameServerFactory server)
    {
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        return socket;
    }

    private static bool Faces(Snapshot snapshot, string id, float facing) =>
        snapshot.Players.Any(p => p.Id == id && p.Facing == facing);

    [Fact] // AK-4, AK-12
    public async Task Fresh_player_gets_threshold_and_default_facing()
    {
        var (socket, id, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = socket;

        var welcome = await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);

        Assert.Equal((float)GameSettings.MinFacingInput, welcome.MinFacingInput);
        Assert.Equal(DefaultFacing, snapshot.Players.Single(p => p.Id == id).Facing);
    }

    [Fact] // AK-8
    public async Task Other_player_sees_facing()
    {
        var (a, aId, _) = await JoinAsync(factory);
        using var _a = a;
        var (b, bId, _) = await JoinAsync(factory);
        using var _b = b;

        await GameSockets.ReceiveSnapshotUntilAsync(b, s => Faces(s, aId, DefaultFacing), Ct);
        await GameSockets.SendInputAsync(a, 1, -1, 0, Ct);
        var seen = await GameSockets.ReceiveSnapshotUntilAsync(b, s => Faces(s, aId, FacingLeft), Ct);

        Assert.Equal(DefaultFacing, seen.Players.Single(p => p.Id == bId).Facing);
    }

    [Fact] // AK-12
    public async Task Server_derives_facing_from_movement_only()
    {
        var (socket, id, _) = await JoinAsync(factory);
        using var _ = socket;

        await GameSockets.SendInputAsync(socket, 1, 0, -1, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => Faces(s, id, FacingUp), Ct);
        await GameSockets.SendInputAsync(socket, 2, 0.005f, 0, Ct);
        var tiny = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.LastProcessedSeq >= 2, Ct);
        var later = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Tick >= tiny.Tick + (uint)World.MaxCoastTicks + 2, Ct);

        Assert.Equal(FacingUp, tiny.Players.Single(p => p.Id == id).Facing);
        Assert.Equal(FacingUp, later.Players.Single(p => p.Id == id).Facing);
    }

    [Fact] // AK-11
    public async Task Rejoin_after_linger_starts_with_default_facing()
    {
        using var observer = await ObserverAsync(factory);
        var (a, id, email) = await JoinAsync(factory);
        using var _ = a;

        await GameSockets.SendInputAsync(a, 1, -1, 0, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(a, s => Faces(s, id, FacingLeft), Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => Faces(s, id, FacingLeft), Ct);
        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.All(p => p.Id != id), Ct);
        using var b = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(b, Ct);
        var first = await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == id && !p.Away), Ct);

        Assert.Equal(DefaultFacing, first.Players.Single(p => p.Id == id).Facing);
    }
}
