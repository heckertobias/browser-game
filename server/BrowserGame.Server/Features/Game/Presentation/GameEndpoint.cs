using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using Google.Protobuf;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Features.Game;

/// <summary>WebSocket <c>/ws</c>: binäre Protobuf-Nachrichten in beide Richtungen.</summary>
internal static partial class GameEndpoint
{
    public static async Task HandleAsync(
        HttpContext context, GameLoop game, UserManager<Account> users, GameDbContext db,
        IConfiguration configuration, IWebHostEnvironment environment, IHostApplicationLifetime lifetime,
        KeyBindingStore keyBindings, ILoggerFactory loggers)
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
        // Auch beim Herunterfahren enden: Kestrel wartet sonst bis zum Shutdown-Timeout auf offene WebSockets, und die Stores
        // (003 AK-19, 011 AK-15) kämen nicht mehr zum Schreiben.
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted, lifetime.ApplicationStopping);
        var aborted = stopping.Token;
        var logger = loggers.CreateLogger(typeof(GameEndpoint));
        var connection = game.Join(
            account.Id, name, saved is null ? null : (saved.X, saved.Y), () => AbortConnection(context),
            KeyBindingRules.FromStored(account.KeyBindings));

        using var receiving = CancellationTokenSource.CreateLinkedTokenSource(aborted);
        var sending = SendLoopAsync(socket, connection, receiving, aborted);
        var (status, reason) = await ReceiveLoopAsync(socket, connection, keyBindings, logger, receiving.Token);

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

    private static void AbortConnection(HttpContext context)
    {
        try
        {
            context.Abort();
        }
        catch (ObjectDisposedException)
        {
            // Der Handler ist schon beendet; der Aufruf kommt vom Tick-Thread und darf ihn nicht abbrechen.
        }
    }

    private static async Task<(WebSocketCloseStatus Status, string Reason)> ReceiveLoopAsync(
        WebSocket socket, PlayerConnection connection, KeyBindingStore keyBindings, ILogger logger, CancellationToken cancellationToken)
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
                else if (message.PayloadCase == ClientMessage.PayloadOneofCase.SendChat)
                {
                    connection.Chat.Writer.TryWrite(message.SendChat.Text);
                }
                else if (message.PayloadCase == ClientMessage.PayloadOneofCase.Attack)
                {
                    connection.Attacks.Writer.TryWrite(message.Attack);
                }
                else if (message.PayloadCase == ClientMessage.PayloadOneofCase.SaveKeyBindings)
                {
                    await SaveKeyBindingsAsync(
                        connection.PlayerId, message.SaveKeyBindings.KeyBindings ?? new KeyBindings(), keyBindings, logger, cancellationToken);
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            return (WebSocketCloseStatus.EndpointUnavailable, "");
        }
    }

    /// <summary>
    /// Prüft und speichert die Belegung (002 AK-9, AK-10). Wird abgewartet: Die nächste Nachricht dieser Verbindung kommt erst
    /// danach dran, und eine Anmeldung auf einem anderen Gerät liest schon den neuen Stand. Abbruch beendet die Verbindung wie
    /// sonst auch; ein Datenbankfehler wird nur geloggt.
    /// </summary>
    private static async Task SaveKeyBindingsAsync(
        string accountId, KeyBindings bindings, KeyBindingStore store, ILogger logger, CancellationToken cancellationToken)
    {
        if (!KeyBindingRules.IsValid(bindings))
        {
            LogKeyBindingsInvalid(logger, accountId);
            return;
        }

        try
        {
            await store.SaveAsync(accountId, KeyBindingRules.ToStored(bindings), cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Der Client hat die Belegung lokal und schickt sie beim nächsten Ändern vollständig erneut.
            LogKeyBindingsSaveFailed(logger, ex, accountId);
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Ungültige Tastenbelegung von {AccountId} verworfen")]
    private static partial void LogKeyBindingsInvalid(ILogger logger, string accountId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Tastenbelegung von {AccountId} nicht gespeichert")]
    private static partial void LogKeyBindingsSaveFailed(ILogger logger, Exception exception, string accountId);

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
