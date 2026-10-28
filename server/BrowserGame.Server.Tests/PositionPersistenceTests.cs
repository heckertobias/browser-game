using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Game;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

public class PositionPersistenceTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static double DistanceFromOrigin(PlayerState p) => Math.Sqrt((double)p.X * p.X + (double)p.Y * p.Y);

    /// <summary>Läuft nach rechts, bis die Figur deutlich außerhalb des Startbereichs ist, und wartet, bis sie steht.</summary>
    private static async Task<PlayerState> WalkAndStopAsync(WebSocket socket, string id, CancellationToken ct)
    {
        var start = (await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), ct)).Players.Single(p => p.Id == id);
        using var moving = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var sending = GameSockets.SendInputsAsync(socket, 1, 0, moving.Token);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id && p.X > start.X + 200), ct);
        await moving.CancelAsync();
        await sending;

        var stopped = await GameSockets.ReceiveSnapshotUntilAsync(socket, _ => true, ct);
        var settled = await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Tick >= stopped.Tick + 20, ct);
        return settled.Players.Single(p => p.Id == id);
    }

    private static async Task<PlayerPosition> WaitForSavedAsync(
        GameServerFactory server, string id, Func<PlayerPosition, bool> expected, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (DateTime.UtcNow < deadline)
        {
            await using (var scope = server.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
                var row = await db.PlayerPositions.AsNoTracking().FirstOrDefaultAsync(p => p.AccountId == id, ct);
                if (row is not null && expected(row))
                {
                    return row;
                }
            }

            await Task.Delay(50, ct);
        }

        Assert.Fail("Position wurde nicht rechtzeitig gespeichert.");
        throw new InvalidOperationException();
    }

    [Fact] // AK-18
    public async Task Position_is_kept_after_leaving_and_coming_back()
    {
        await using var server = new GameServerFactory { PositionSaveIntervalSeconds = "3600" };
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(server);
        var id = await GameSockets.AccountIdAsync(server, account.Email);
        var cookieA = await GameSockets.SignInCookieAsync(server, account.Email);
        var cookieB = await GameSockets.SignInCookieAsync(server, account.Email);

        using var a = await GameSockets.ConnectAsync(server, cookieA, GameSockets.Origin, Ct);
        var left = await WalkAndStopAsync(a, id, Ct);
        await a.CloseAsync(WebSocketCloseStatus.NormalClosure, "", Ct);
        await WaitForSavedAsync(server, id, p => (float)p.X == left.X && (float)p.Y == left.Y, Ct);

        using var b = await GameSockets.ConnectAsync(server, cookieB, GameSockets.Origin, Ct);
        var back = (await GameSockets.ReceiveSnapshotUntilAsync(b, s => s.Players.Any(p => p.Id == id), Ct)).Players.Single(p => p.Id == id);
        Assert.Equal((left.X, left.Y), (back.X, back.Y));
        Assert.True(DistanceFromOrigin(back) > World.SpawnRadius);
    }

    [Fact] // AK-19
    public async Task Positions_are_saved_while_playing()
    {
        await using var server = new GameServerFactory { PositionSaveIntervalSeconds = "0.2" };
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(server);
        var id = await GameSockets.AccountIdAsync(server, account.Email);
        using var socket = await GameSockets.ConnectAsync(server, await GameSockets.SignInCookieAsync(server, account.Email), GameSockets.Origin, Ct);

        var here = await WalkAndStopAsync(socket, id, Ct);

        await WaitForSavedAsync(server, id, p => (float)p.X == here.X && (float)p.Y == here.Y, Ct);
    }

    [Fact] // AK-19
    public async Task Restart_keeps_last_position()
    {
        await using var first = new GameServerFactory { KeepDatabase = true, PositionSaveIntervalSeconds = "3600", LingerSeconds = "60" };
        await using var second = new GameServerFactory { ConnectionString = first.ConnectionString };
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(first);
        var id = await GameSockets.AccountIdAsync(first, account.Email);

        PlayerState last;
        using (var socket = await GameSockets.ConnectAsync(first, await GameSockets.SignInCookieAsync(first, account.Email), GameSockets.Origin, Ct))
        {
            last = await WalkAndStopAsync(socket, id, Ct);
        }

        await first.DisposeAsync();

        using var again = await GameSockets.ConnectAsync(second, await GameSockets.SignInCookieAsync(second, account.Email), GameSockets.Origin, Ct);
        var back = (await GameSockets.ReceiveSnapshotUntilAsync(again, s => s.Players.Any(p => p.Id == id), Ct)).Players.Single(p => p.Id == id);
        Assert.Equal((last.X, last.Y), (back.X, back.Y));
        Assert.True(DistanceFromOrigin(back) > World.SpawnRadius);
    }
}
