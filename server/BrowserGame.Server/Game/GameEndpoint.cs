using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Game;

/// <summary>WebSocket <c>/ws</c>: binäre Protobuf-Nachrichten in beide Richtungen.</summary>
internal static class GameEndpoint
{
    public static async Task HandleAsync(
        HttpContext context, GameLoop game, UserManager<Account> users, GameDbContext db,
        IConfiguration configuration, IWebHostEnvironment environment)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        // 401 statt Redirect zur Anmeldung: WebSocket-Clients folgen keinem Redirect.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        Uri.TryCreate(configuration["App:PublicUrl"], UriKind.Absolute, out var publicUrl);
        var requestOrigin = new Uri($"{context.Request.Scheme}://{context.Request.Host}");
        if (!GameAccess.IsAllowedOrigin(context.Request.Headers.Origin.ToString(), publicUrl, environment.IsDevelopment(), requestOrigin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var account = await users.GetUserAsync(context.User);
        if (account is not { EmailConfirmed: true, UserName: { } name })
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var saved = await db.PlayerPositions.AsNoTracking().FirstOrDefaultAsync(p => p.AccountId == account.Id, context.RequestAborted);

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var aborted = context.RequestAborted;
        var connection = game.Join(account.Id, name, saved is null ? null : (saved.X, saved.Y));

        using var receiving = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        var sending = SendLoopAsync(socket, connection, receiving, aborted);
        var (status, reason) = await ReceiveLoopAsync(socket, connection, receiving.Token);

        game.Leave(connection); // beendet auch die Sende-Schleife
        await sending;

        if (socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            try
            {
                await socket.CloseOutputAsync(status, reason, CancellationToken.None);
            }
            catch (WebSocketException)
            {
                // Client ist bereits weg.
            }
        }
    }

    private static async Task<(WebSocketCloseStatus Status, string Reason)> ReceiveLoopAsync(
        WebSocket socket, PlayerConnection connection, CancellationToken cancellationToken)
    {
        var buffer = new byte[GameSettings.MaxClientMessageBytes];
        try
        {
            while (true)
            {
                var result = await socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return (WebSocketCloseStatus.NormalClosure, "");
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    return (WebSocketCloseStatus.InvalidMessageType, "binary only");
                }

                if (!result.EndOfMessage)
                {
                    return (WebSocketCloseStatus.MessageTooBig, "message too big");
                }

                ClientMessage message;
                try
                {
                    message = ClientMessage.Parser.ParseFrom(buffer, 0, result.Count);
                }
                catch (InvalidProtocolBufferException)
                {
                    return (WebSocketCloseStatus.PolicyViolation, "invalid message");
                }

                if (message.PayloadCase == ClientMessage.PayloadOneofCase.Input)
                {
                    var input = message.Input;
                    connection.Inbox.Writer.TryWrite(new MoveInput(input.Seq, input.DirX, input.DirY));
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            return (WebSocketCloseStatus.EndpointUnavailable, "");
        }
    }

    private static readonly TimeSpan KickCloseTimeout = TimeSpan.FromSeconds(5);

    private static async Task SendLoopAsync(
        WebSocket socket, PlayerConnection connection, CancellationTokenSource receiving, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var bytes in connection.Outbox.Reader.ReadAllAsync(cancellationToken))
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
            }

            if (connection.WasKicked && socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "kicked", CancellationToken.None);
                // Antwortet der Client nicht mit Close, endet die Empfangs-Schleife nach dem Timeout.
                receiving.CancelAfter(KickCloseTimeout);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // Verbindung abgebrochen; die Empfangs-Schleife meldet den Spieler ab.
        }
    }
}
