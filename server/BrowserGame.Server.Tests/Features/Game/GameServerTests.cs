using System.Net;
using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;

namespace BrowserGame.Server.Tests;

public sealed class GameServerTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly TimeSpan OneSecond = TimeSpan.FromSeconds(1);

    [Fact] // AK-1
    public async Task Landing_page_links_to_game()
    {
        var html = await factory.CreateClient().GetStringAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Contains("href=\"/play\"", html, StringComparison.Ordinal);
        Assert.Contains("Spielen", html, StringComparison.Ordinal);
    }

    [Theory] // AK-1
    [InlineData("/play")]
    [InlineData("/play/")]
    [InlineData("/play/irgendwas")]
    public async Task Play_redirects_anonymous_to_login(string path)
    {
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Login", response.Headers.Location?.AbsolutePath);
        Assert.Contains("ReturnUrl=", response.Headers.Location?.Query, StringComparison.Ordinal);
    }

    [Theory] // AK-1
    [InlineData("/play")]
    [InlineData("/play/")]
    [InlineData("/play/irgendwas")]
    public async Task Play_serves_client_when_signed_in(string path)
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.LoginAsync(client, account.Email);

        var (response, html) = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", html, StringComparison.Ordinal);
    }

    [Fact] // AK-1: Assets dürfen nicht vom SPA-Fallback (index.html) verdeckt werden
    public async Task Play_assets_are_served_as_files()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/play/assets/test-asset.js", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("test-asset", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Missing_play_asset_is_not_found()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/play/assets/fehlt.js", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Health_is_ok()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact] // AK-1
    public async Task Joining_player_gets_welcome_and_appears_in_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        using var socket = await ConnectAsync(ct);

        var welcome = (await ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome, ct)).Welcome;
        Assert.False(string.IsNullOrEmpty(welcome.Name));
        Assert.Equal(GameSettings.TickRateHz, (int)welcome.TickRateHz);

        await ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == welcome.PlayerId), ct);
    }

    [Fact] // AK-3
    public async Task Other_player_sees_movement_within_one_second()
    {
        var ct = TestContext.Current.CancellationToken;
        using var a = await ConnectAsync(ct);
        using var b = await ConnectAsync(ct);
        var aId = (await ReceiveUntilAsync(a, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome, ct)).Welcome.PlayerId;
        var start = (await ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == aId), ct)).Players.Single(p => p.Id == aId);

        using var moving = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sending = SendInputsAsync(a, 1, 0, moving.Token);
        var startedAt = TimeProvider.System.GetTimestamp();

        await ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == aId && p.X > start.X + 10), ct);
        Assert.True(TimeProvider.System.GetElapsedTime(startedAt) < OneSecond);

        await moving.CancelAsync();
        await sending;
    }

    [Fact] // AK-4
    public async Task Player_disappears_after_closing()
    {
        var ct = TestContext.Current.CancellationToken;
        using var a = await ConnectAsync(ct);
        using var b = await ConnectAsync(ct);
        var aId = (await ReceiveUntilAsync(a, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome, ct)).Welcome.PlayerId;
        await ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == aId), ct);

        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", ct);
        var closedAt = TimeProvider.System.GetTimestamp();

        await ReceiveSnapshotUntilAsync(b, s => s.Players.All(p => p.Id != aId), ct);
        Assert.True(TimeProvider.System.GetElapsedTime(closedAt) < OneSecond);
    }

    [Fact] // AK-5: zu große Richtung bringt nicht mehr als die erlaubte Geschwindigkeit
    public async Task Oversized_input_is_limited_to_allowed_speed()
    {
        var ct = TestContext.Current.CancellationToken;
        using var a = await ConnectAsync(ct);
        var aId = (await ReceiveUntilAsync(a, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome, ct)).Welcome.PlayerId;
        var first = await ReceiveSnapshotUntilAsync(a, s => s.Players.Any(p => p.Id == aId), ct);

        using var moving = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sending = SendInputsAsync(a, 1000, 0, moving.Token);
        var later = await ReceiveSnapshotUntilAsync(a, s => s.Tick >= first.Tick + 10, ct);
        await moving.CancelAsync();
        await sending;

        var maxDistance = (later.Tick - first.Tick) * GameSettings.Speed / GameSettings.TickRateHz;
        var moved = later.Players.Single(p => p.Id == aId).X - first.Players.Single(p => p.Id == aId).X;
        Assert.True(moved > 0);
        Assert.True(moved <= maxDistance + 0.01, $"moved {moved} > allowed {maxDistance}");
    }

    [Fact]
    public async Task Invalid_message_closes_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var socket = await ConnectAsync(ct);

        await socket.SendAsync(new byte[] { 0xFF, 0xFF, 0xFF }, WebSocketMessageType.Binary, true, ct);

        Assert.Equal(WebSocketCloseStatus.PolicyViolation, await ReceiveCloseStatusAsync(socket, ct));
    }

    [Fact]
    public async Task Oversized_message_closes_connection()
    {
        var ct = TestContext.Current.CancellationToken;
        using var socket = await ConnectAsync(ct);

        await socket.SendAsync(new byte[GameSettings.MaxClientMessageBytes + 1], WebSocketMessageType.Binary, true, ct);

        Assert.Equal(WebSocketCloseStatus.MessageTooBig, await ReceiveCloseStatusAsync(socket, ct));
    }

    private async Task<WebSocket> ConnectAsync(CancellationToken ct) => (await GameSockets.ConnectNewPlayerAsync(factory, ct)).Socket;

    private static async Task SendInputsAsync(WebSocket socket, float dirX, float dirY, CancellationToken ct)
    {
        uint seq = 0;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var message = new ClientMessage { Input = new Input { Seq = ++seq, DirX = dirX, DirY = dirY } };
                await socket.SendAsync(message.ToByteArray(), WebSocketMessageType.Binary, true, ct);
                await Task.Delay(1000 / GameSettings.TickRateHz, ct);
            }
        }
        catch (OperationCanceledException)
        {
            // Test ist fertig.
        }
    }

    private static async Task<Snapshot> ReceiveSnapshotUntilAsync(WebSocket socket, Func<Snapshot, bool> predicate, CancellationToken ct) =>
        (await ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot && predicate(m.Snapshot), ct)).Snapshot;

    /// <summary>Liest Nachrichten, bis eine passt; spätestens nach 2 s schlägt der Test fehl.</summary>
    private static async Task<ServerMessage> ReceiveUntilAsync(WebSocket socket, Func<ServerMessage, bool> predicate, CancellationToken ct)
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

    private static async Task<WebSocketCloseStatus?> ReceiveCloseStatusAsync(WebSocket socket, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var buffer = new byte[64 * 1024];
        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, timeout.Token);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                return result.CloseStatus;
            }
        }
    }
}
