using System.Net;
using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;

namespace BrowserGame.Server.Tests;

/// <summary>Angemeldete WebSocket-Verbindungen zum Testserver und das Lesen und Senden von Spielnachrichten.</summary>
public static class GameSockets
{
    /// <summary>Basisadresse des Testservers.</summary>
    public const string Origin = "http://localhost";

    public static async Task<string> SignInCookieAsync(GameServerFactory factory, string email)
    {
        using var client = factory.CreateFormClient();
        var (response, _) = await AccountTestFlows.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bg.auth=", StringComparison.Ordinal)).Split(';')[0];
    }

    public static async Task<WebSocket> ConnectAsync(GameServerFactory factory, string? cookie, string? origin, CancellationToken ct)
    {
        var uri = new UriBuilder(factory.Server.BaseAddress) { Scheme = "ws", Path = "/ws" }.Uri;
        var client = factory.Server.CreateWebSocketClient();
        client.ConfigureRequest = request =>
        {
            if (cookie is not null)
            {
                request.Headers.Cookie = cookie;
            }

            if (origin is not null)
            {
                request.Headers.Origin = origin;
            }
        };
        return await client.ConnectAsync(uri, ct);
    }

    public static async Task<(WebSocket Socket, string AccountId, AccountTestFlows.CreatedAccount Account)> ConnectNewPlayerAsync(
        GameServerFactory factory, CancellationToken ct)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var cookie = await SignInCookieAsync(factory, account.Email);
        var socket = await ConnectAsync(factory, cookie, Origin, ct);
        return (socket, await AccountIdAsync(factory, account.Email), account);
    }

    public static async Task<string> AccountIdAsync(GameServerFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        return (await users.FindByEmailAsync(email))!.Id;
    }

    public static async Task SeedPositionAsync(GameServerFactory factory, string accountId, double x, double y)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        db.PlayerPositions.Add(new PlayerPosition { AccountId = accountId, X = x, Y = y, UpdatedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
    }

    public static async Task<string> HandshakeFailureAsync(GameServerFactory factory, string? cookie, string? origin, CancellationToken ct) =>
        (await Assert.ThrowsAsync<InvalidOperationException>(() => ConnectAsync(factory, cookie, origin, ct))).Message;

    public static async Task<Welcome> ReceiveWelcomeAsync(WebSocket socket, CancellationToken ct) =>
        (await ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome, ct)).Welcome;

    public static async Task<Snapshot> ReceiveSnapshotUntilAsync(WebSocket socket, Func<Snapshot, bool> predicate, CancellationToken ct) =>
        (await ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot && predicate(m.Snapshot), ct)).Snapshot;

    /// <summary>Liest Nachrichten, bis eine passt; spätestens nach 2 s schlägt der Test fehl.</summary>
    public static async Task<ServerMessage> ReceiveUntilAsync(WebSocket socket, Func<ServerMessage, bool> predicate, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            Assert.True(result.EndOfMessage, "Nachricht unerwartet fragmentiert");
            var message = ServerMessage.Parser.ParseFrom(buffer, 0, result.Count);
            if (predicate(message))
            {
                return message;
            }
        }
    }

    public static Task SendInputAsync(WebSocket socket, uint seq, float dirX, float dirY, CancellationToken ct) =>
        socket.SendAsync(
            new ClientMessage { Input = new Input { Seq = seq, DirX = dirX, DirY = dirY } }.ToByteArray(),
            WebSocketMessageType.Binary, true, ct);

    public static async Task SendInputsAsync(WebSocket socket, float dirX, float dirY, CancellationToken ct)
    {
        uint seq = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await SendInputAsync(socket, ++seq, dirX, dirY, ct);
                await Task.Delay(1000 / GameSettings.TickRateHz, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Test ist fertig.
        }
    }
}
