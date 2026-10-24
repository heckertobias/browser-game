using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using Google.Protobuf;

namespace BrowserGame.Server.Game;

/// <summary>WebSocket <c>/ws</c>: binäre Protobuf-Nachrichten in beide Richtungen.</summary>
internal static class GameEndpoint
{
    public static async Task HandleAsync(HttpContext context, GameLoop game)
    {
        if (!context.WebSockets.IsWebSocketRequest)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        using var socket = await context.WebSockets.AcceptWebSocketAsync();
        var aborted = context.RequestAborted;
        var connection = game.Join();

        var sending = SendLoopAsync(socket, connection, aborted);
        var (status, reason) = await ReceiveLoopAsync(socket, connection, aborted);

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

    private static async Task SendLoopAsync(WebSocket socket, PlayerConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await foreach (var bytes in connection.Outbox.Reader.ReadAllAsync(cancellationToken))
            {
                await socket.SendAsync(bytes, WebSocketMessageType.Binary, endOfMessage: true, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
        {
            // Verbindung abgebrochen; die Empfangs-Schleife meldet den Spieler ab.
        }
    }
}
