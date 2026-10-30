using System.Net.WebSockets;
using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Tests;

public sealed class GameSessionTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Wartet auf Kicked; danach muss der Server den Socket mit NormalClosure schließen.</summary>
    private static async Task<Kicked.Types.Reason> ReceiveKickAsync(WebSocket socket, CancellationToken ct)
    {
        var kicked = (await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Kicked, ct)).Kicked;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var result = await socket.ReceiveAsync(new byte[1024], timeout.Token);
        Assert.Equal(WebSocketMessageType.Close, result.MessageType);
        Assert.Equal(WebSocketCloseStatus.NormalClosure, result.CloseStatus);
        return kicked.Reason;
    }

    private Task<WebSocket> ObserverAsync() => ObserverAsync(factory);

    private static async Task<WebSocket> ObserverAsync(GameServerFactory server)
    {
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        return socket;
    }

    /// <summary>Liest Snapshots, bis die Figur fehlt; sie darf dabei nie als wartend gezeigt worden sein.</summary>
    private static async Task AssertGoneAtOnceAsync(WebSocket observer, string id)
    {
        var sawAway = false;
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s =>
        {
            var figure = s.Players.SingleOrDefault(p => p.Id == id);
            sawAway |= figure?.Away == true;
            return figure is null;
        }, Ct);
        Assert.False(sawAway);
    }

    [Fact] // AK-12
    public async Task Sign_in_elsewhere_kicks_old_device_and_keeps_one_figure()
    {
        using var observer = await ObserverAsync();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        using var a = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        Assert.Equal(id, (await GameSockets.ReceiveWelcomeAsync(a, Ct)).PlayerId);
        using var b = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        Assert.Equal(id, (await GameSockets.ReceiveWelcomeAsync(b, Ct)).PlayerId);

        Assert.Equal(Kicked.Types.Reason.SignedInElsewhere, await ReceiveKickAsync(a, Ct));

        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct);
        var figure = Assert.Single(snapshot.Players, p => p.Id == id);
        Assert.False(figure.Away);
    }

    [Fact] // AK-20
    public async Task Closed_tab_leaves_waiting_figure()
    {
        using var observer = await ObserverAsync();
        var (a, id, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = a;
        await GameSockets.ReceiveWelcomeAsync(a, Ct);

        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);

        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id && p.Away), Ct);
    }

    [Fact] // AK-21
    public async Task Reconnect_within_linger_takes_over_same_figure()
    {
        await using var slow = new GameServerFactory { LingerSeconds = "10" };
        using var observer = await ObserverAsync(slow);
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(slow);
        var id = await GameSockets.AccountIdAsync(slow, account.Email);
        var cookieA = await GameSockets.SignInCookieAsync(slow, account.Email);
        var cookieB = await GameSockets.SignInCookieAsync(slow, account.Email);
        using var a = await GameSockets.ConnectAsync(slow, cookieA, GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(a, Ct);
        var before = (await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct)).Players.Single(p => p.Id == id);

        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id && p.Away), Ct);
        using var b = await GameSockets.ConnectAsync(slow, cookieB, GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(b, Ct);

        var after = (await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id && !p.Away), Ct)).Players.Single(p => p.Id == id);
        Assert.Equal((before.X, before.Y), (after.X, after.Y));
        var next = await GameSockets.ReceiveUntilAsync(b, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot, Ct);
        Assert.Contains(next.Snapshot.Players, p => p.Id == id && !p.Away);
    }

    [Fact] // AK-22, AK-13
    public async Task Logout_kicks_signed_out_and_removes_figure_at_once()
    {
        using var observer = await ObserverAsync();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        using var client = factory.CreateFormClient();
        await AccountTestFlows.LoginAsync(client, account.Email);
        using var socket = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct);

        await client.PostAsync("/", "/Account/Logout", new Dictionary<string, string>());

        Assert.Equal(Kicked.Types.Reason.SignedOut, await ReceiveKickAsync(socket, Ct));
        await AssertGoneAtOnceAsync(observer, id);
    }

    [Fact] // AK-15
    public async Task Password_reset_kicks_running_game()
    {
        using var observer = await ObserverAsync();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        using var socket = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct);

        using var client = factory.CreateFormClient();
        await AccountTestFlows.ForgotPasswordAsync(client, account.Email);
        var link = AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(account.Email)).Text);
        await AccountTestFlows.ResetPasswordAsync(client, link, "ein-ganz-neues-passwort");

        Assert.Equal(Kicked.Types.Reason.SignedOut, await ReceiveKickAsync(socket, Ct));
        await AssertGoneAtOnceAsync(observer, id);
    }

    [Fact] // AK-16
    public async Task Delete_kicks_signed_out_and_removes_figure()
    {
        using var observer = await ObserverAsync();
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        using var socket = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == id), Ct);

        await client.PostAsync("/Account/Delete", "/Account/Delete", new Dictionary<string, string> { ["Password"] = AccountTestFlows.Password });

        Assert.Equal(Kicked.Types.Reason.SignedOut, await ReceiveKickAsync(socket, Ct));
        await AssertGoneAtOnceAsync(observer, id);
    }
}
