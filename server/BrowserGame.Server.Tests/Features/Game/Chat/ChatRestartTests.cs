using System.Diagnostics;
using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using Microsoft.Extensions.Hosting;

namespace BrowserGame.Server.Tests;

/// <summary>011 T6: Verlauf über einen echten Neustart des Servers; dieselbe Datenbank, offene Sockets.</summary>
public sealed class ChatRestartTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-15, AK-11
    public async Task Restart_with_open_socket_writes_last_messages_and_next_player_gets_them_after_welcome()
    {
        await using var first = new GameServerFactory { KeepDatabase = true };
        await using var second = new GameServerFactory { ConnectionString = first.ConnectionString };
        var (socket, _, account) = await GameSockets.ConnectNewPlayerAsync(first, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);

        await GameSockets.SendChatAsync(socket, "eins", Ct);
        await GameSockets.SendChatAsync(socket, "zwei", Ct);
        await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && m.ChatMessage.Text == "zwei", Ct);

        await first.DisposeAsync();

        using var again = await GameSockets.ConnectAsync(second, await GameSockets.SignInCookieAsync(second, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(again, Ct);
        var next = await GameSockets.ReceiveUntilAsync(again, m => m.PayloadCase != ServerMessage.PayloadOneofCase.Snapshot, Ct);
        Assert.Equal(ServerMessage.PayloadOneofCase.ChatHistory, next.PayloadCase);
        Assert.Equal(["eins", "zwei"], next.ChatHistory.Messages.Select(m => m.Text));
        Assert.All(next.ChatHistory.Messages, m => Assert.Equal(account.Name, m.SenderName));
    }

    // Kestrel wartet sonst bis zum Shutdown-Timeout auf offene WebSockets, danach schreibt kein Store mehr. Der TestServer wartet
    // nicht, deshalb prüft dieser Test den Handler direkt: Der Socket endet, statt bis zum Limit stumm zu bleiben.
    [Fact] // AK-15
    public async Task Stopping_the_host_ends_open_sockets_at_once()
    {
        await using var server = new GameServerFactory();
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);

        var watch = Stopwatch.StartNew();
        server.Services.GetRequiredService<IHostApplicationLifetime>().StopApplication();

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        limit.CancelAfter(TimeSpan.FromSeconds(10));
        var buffer = new byte[64 * 1024];
        var ended = false;
        while (!ended)
        {
            try
            {
                var result = await socket.ReceiveAsync(buffer, limit.Token);
                ended = result.MessageType == WebSocketMessageType.Close;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !limit.IsCancellationRequested)
            {
                ended = true;
            }
        }

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"{watch.Elapsed}");
    }
}
