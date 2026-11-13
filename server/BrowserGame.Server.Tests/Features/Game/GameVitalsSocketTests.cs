using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class GameVitalsSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const float MaxHp = (float)GameSettings.MaxHp;
    private const float MaxMana = (float)GameSettings.MaxMana;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GameLoop Game(GameServerFactory server) => server.Services.GetRequiredService<GameLoop>();

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

    [Fact] // AK-2, AK-5
    public async Task Fresh_player_gets_max_values_and_starts_full()
    {
        var (socket, id, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = socket;

        var welcome = await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);

        Assert.Equal(MaxHp, welcome.MaxHp);
        Assert.Equal(MaxMana, welcome.MaxMana);
        Assert.Equal(MaxHp, snapshot.Hp);
        Assert.Equal(MaxMana, snapshot.Mana);
        Assert.Equal(0u, snapshot.Players.Single(p => p.Id == id).SpawnSeq);
    }

    [Fact] // AK-6, AK-10
    public async Task Set_vitals_reaches_own_snapshot_and_mana_regenerates()
    {
        var (socket, id, _) = await JoinAsync(factory);
        using var _ = socket;

        Game(factory).SetVitals(id, hp: 30, mana: 20);
        var changed = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Hp == 30f, Ct);
        var later = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Mana > changed.Mana, Ct);

        Assert.InRange(changed.Mana, 20f, 25f);
        Assert.Equal(30f, later.Hp);
    }

    [Fact] // AK-5
    public async Task Other_player_keeps_own_values()
    {
        var (a, aId, _) = await JoinAsync(factory);
        using var _a = a;
        var (b, _, _) = await JoinAsync(factory);
        using var _b = b;

        Game(factory).SetVitals(aId, hp: 30);
        var seenByA = await GameSockets.ReceiveSnapshotUntilAsync(a, s => s.Hp == 30f, Ct);
        var seenByB = await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Tick >= seenByA.Tick, Ct);

        Assert.Equal(MaxHp, seenByB.Hp);
    }

    [Fact] // AK-6, AK-8
    public async Task Death_respawns_full_with_new_spawn_seq()
    {
        var (socket, id, _) = await JoinAsync(factory);
        using var _ = socket;

        Game(factory).SetVitals(id, hp: 0);
        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id && p.SpawnSeq == 1), Ct);

        Assert.Equal(MaxHp, snapshot.Hp);
        Assert.Equal(MaxMana, snapshot.Mana);
    }

    [Fact] // AK-2
    public async Task Reconnect_within_linger_keeps_lowered_vitals()
    {
        await using var slow = new GameServerFactory { LingerSeconds = "10" };
        using var observer = await ObserverAsync(slow);
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(slow);
        var id = await GameSockets.AccountIdAsync(slow, account.Email);
        var cookieA = await GameSockets.SignInCookieAsync(slow, account.Email);
        var cookieB = await GameSockets.SignInCookieAsync(slow, account.Email);
        using var a = await GameSockets.ConnectAsync(slow, cookieA, GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(a, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(a, s => s.Players.Any(p => p.Id == id), Ct);
        Game(slow).SetVitals(id, hp: 40, mana: 10);
        await GameSockets.ReceiveSnapshotUntilAsync(a, s => s.Hp == 40f, Ct);

        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id && p.Away), Ct);
        using var b = await GameSockets.ConnectAsync(slow, cookieB, GameSockets.Origin, Ct);
        var welcome = await GameSockets.ReceiveWelcomeAsync(b, Ct);
        var first = await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == id && !p.Away), Ct);

        Assert.Equal(MaxHp, welcome.MaxHp);
        Assert.Equal(40f, first.Hp);
        Assert.True(first.Mana < MaxMana);
    }

    [Fact] // AK-2
    public async Task Rejoin_after_linger_starts_full()
    {
        using var observer = await ObserverAsync(factory);
        var (a, id, email) = await JoinAsync(factory);
        using var _ = a;
        Game(factory).SetVitals(id, hp: 40, mana: 10);
        await GameSockets.ReceiveSnapshotUntilAsync(a, s => s.Hp == 40f, Ct);

        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct);
        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.All(p => p.Id != id), Ct);
        using var b = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(b, Ct);
        var first = await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == id && !p.Away), Ct);

        Assert.Equal(MaxHp, first.Hp);
        Assert.Equal(MaxMana, first.Mana);
    }
}
