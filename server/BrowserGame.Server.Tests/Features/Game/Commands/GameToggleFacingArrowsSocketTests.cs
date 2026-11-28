using System.Net.WebSockets;
using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Tests;

/// <summary>017 T3: toggleFacingArrows über echte WebSockets (SQLite, in der CI auch PostgreSQL).</summary>
public sealed class GameToggleFacingArrowsSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string Sentinel = "sentinel";
    private const string Toggle = "#toggle-facing-arrows";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10); // Nur Fehlergrenze für langsame Runner.

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record Joined(WebSocket Socket, string Id, string Name) : IDisposable
    {
        public void Dispose() => Socket.Dispose();
    }

    private async Task<Joined> JoinAsync()
    {
        var (socket, id, account) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return new Joined(socket, id, account.Name);
    }

    // Pflichtnachrichten kommen FIFO; eine Antwort, ein Echo oder eine Ablehnung des Commands stünde vor dem Sentinel.
    private static async Task<List<string>> ReadUntilSentinelAsync(WebSocket socket, string senderName)
    {
        var seen = new List<string>();
        await GameSockets.ReceiveUntilAsync(socket, m =>
        {
            switch (m.PayloadCase)
            {
                case ServerMessage.PayloadOneofCase.ChatMessage when m.ChatMessage.SenderName == senderName:
                    seen.Add(m.ChatMessage.Text);
                    return m.ChatMessage.Text == Sentinel;
                case ServerMessage.PayloadOneofCase.ToggleFacingArrows:
                    seen.Add(Toggle);
                    break;
                case ServerMessage.PayloadOneofCase.ChatRejected:
                    seen.Add("#chat-rejected");
                    break;
                case ServerMessage.PayloadOneofCase.CommandRejected:
                    seen.Add($"#command-rejected:{m.CommandRejected.Reason}");
                    break;
            }

            return false;
        }, Limit, Ct);
        return seen;
    }

    [Fact] // AK-11, AK-12
    public async Task Toggle_reaches_only_the_sender_once_per_command_without_chat_echo()
    {
        using var a = await JoinAsync();
        using var b = await JoinAsync();

        await GameSockets.SendChatAsync(a.Socket, "/run toggleFacingArrows()", Ct);
        await GameSockets.SendChatAsync(a.Socket, "/run toggleFacingArrows()", Ct);
        await GameSockets.SendChatAsync(a.Socket, Sentinel, Ct);

        Assert.Equal([Toggle, Toggle, Sentinel], await ReadUntilSentinelAsync(a.Socket, a.Name));
        Assert.Equal([Sentinel], await ReadUntilSentinelAsync(b.Socket, a.Name));
    }
}
