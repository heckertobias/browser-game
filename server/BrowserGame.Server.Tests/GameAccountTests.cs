using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Game;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

public sealed class GameAccountTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private async Task<(string Email, string Cookie)> SignedInAccountAsync()
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        return (account.Email, await GameSockets.SignInCookieAsync(factory, account.Email));
    }

    [Fact]
    public async Task Anonymous_connection_is_rejected()
    {
        var message = await GameSockets.HandshakeFailureAsync(factory, null, GameSockets.Origin, TestContext.Current.CancellationToken);

        Assert.Contains("status code: 401", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Foreign_origin_is_rejected()
    {
        var (_, cookie) = await SignedInAccountAsync();

        var message = await GameSockets.HandshakeFailureAsync(factory, cookie, "https://evil.example", TestContext.Current.CancellationToken);

        Assert.Contains("status code: 403", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_origin_is_rejected()
    {
        var (_, cookie) = await SignedInAccountAsync();

        var message = await GameSockets.HandshakeFailureAsync(factory, cookie, null, TestContext.Current.CancellationToken);

        Assert.Contains("status code: 403", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Unconfirmed_account_is_rejected()
    {
        var (email, cookie) = await SignedInAccountAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var user = (await users.FindByEmailAsync(email))!;
            user.EmailConfirmed = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }

        var message = await GameSockets.HandshakeFailureAsync(factory, cookie, GameSockets.Origin, TestContext.Current.CancellationToken);

        Assert.Contains("status code: 401", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Deleted_account_is_rejected()
    {
        var (email, cookie) = await SignedInAccountAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            Assert.True((await users.DeleteAsync((await users.FindByEmailAsync(email))!)).Succeeded);
        }

        var message = await GameSockets.HandshakeFailureAsync(factory, cookie, GameSockets.Origin, TestContext.Current.CancellationToken);

        Assert.Contains("status code: 401", message, StringComparison.Ordinal);
    }

    [Fact] // AK-8
    public async Task Player_id_and_name_come_from_account()
    {
        var ct = TestContext.Current.CancellationToken;
        var (socket, accountId, account) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _ = socket;

        var welcome = await GameSockets.ReceiveWelcomeAsync(socket, ct);

        Assert.Equal(accountId, welcome.PlayerId);
        Assert.Equal(account.Name, welcome.Name);
    }

    [Fact] // AK-8
    public async Task Others_see_my_account_name()
    {
        var ct = TestContext.Current.CancellationToken;
        var (a, aId, aAccount) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _a = a;
        var (b, _, _) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _b = b;

        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == aId), ct);

        Assert.Equal(aAccount.Name, snapshot.Players.Single(p => p.Id == aId).Name);
    }

    [Fact] // AK-17
    public async Task First_join_starts_at_start_point()
    {
        var ct = TestContext.Current.CancellationToken;
        var (socket, accountId, _) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _ = socket;

        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == accountId), ct);

        var me = snapshot.Players.Single(p => p.Id == accountId);
        Assert.True(Math.Sqrt((double)me.X * me.X + (double)me.Y * me.Y) <= World.SpawnRadius + 0.01);
    }

    [Fact] // AK-18
    public async Task Saved_position_is_restored_on_any_device()
    {
        var ct = TestContext.Current.CancellationToken;
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var accountId = await GameSockets.AccountIdAsync(factory, account.Email);
        // Die Position ist hier vorab gespeichert.
        await GameSockets.SeedPositionAsync(factory, accountId, 1234.5, -678.25);

        using var deviceA = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, ct);
        var onA = (await GameSockets.ReceiveSnapshotUntilAsync(deviceA, s => s.Players.Any(p => p.Id == accountId), ct)).Players.Single(p => p.Id == accountId);
        Assert.Equal(1234.5f, onA.X);
        Assert.Equal(-678.25f, onA.Y);
        await deviceA.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);

        using var deviceB = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, ct);
        var onB = (await GameSockets.ReceiveSnapshotUntilAsync(deviceB, s => s.Players.Any(p => p.Id == accountId), ct)).Players.Single(p => p.Id == accountId);
        Assert.Equal(1234.5f, onB.X);
        Assert.Equal(-678.25f, onB.Y);
    }

    [Fact]
    public async Task Color_is_derived_from_account()
    {
        var ct = TestContext.Current.CancellationToken;
        var (socket, accountId, _) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _ = socket;

        var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == accountId), ct);

        Assert.Equal(PlayerColors.For(accountId), snapshot.Players.Single(p => p.Id == accountId).Color);
    }

    // Doppelter Beitritt: Spielschleife läuft weiter, neueste Verbindung steuert.
    [Fact]
    public async Task Newer_connection_of_same_account_controls_the_figure()
    {
        var ct = TestContext.Current.CancellationToken;
        var (observer, _, _) = await GameSockets.ConnectNewPlayerAsync(factory, ct);
        using var _o = observer;
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var accountId = await GameSockets.AccountIdAsync(factory, account.Email);

        using var x1 = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, ct);
        await GameSockets.SendInputAsync(x1, 1_000_000, 0, 0, ct);
        await GameSockets.ReceiveSnapshotUntilAsync(x1, s => s.LastProcessedSeq == 1_000_000, ct);

        using var x2 = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, ct);
        Assert.Equal(accountId, (await GameSockets.ReceiveWelcomeAsync(x2, ct)).PlayerId);
        var start = (await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == accountId), ct)).Players.Single(p => p.Id == accountId);

        using var moving = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sending = GameSockets.SendInputsAsync(x2, 1, 0, moving.Token);
        await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Players.Any(p => p.Id == accountId && p.X > start.X + 10), ct);
        await moving.CancelAsync();
        await sending;

        await x1.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
        var afterClose = await GameSockets.ReceiveSnapshotUntilAsync(observer, _ => true, ct);
        var later = await GameSockets.ReceiveSnapshotUntilAsync(observer, s => s.Tick >= afterClose.Tick + 10, ct);

        Assert.Contains(later.Players, p => p.Id == accountId);
    }
}
