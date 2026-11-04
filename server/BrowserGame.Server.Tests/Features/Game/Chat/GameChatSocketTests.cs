using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Tests;

public sealed class GameChatSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-10
    public async Task Chat_over_socket_reaches_all_players_including_sender()
    {
        var (a, _, accountA) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        var (b, _, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        await GameSockets.ReceiveWelcomeAsync(a, Ct);
        await GameSockets.ReceiveWelcomeAsync(b, Ct);

        await GameSockets.SendChatAsync(a, "ping", Ct);

        foreach (var socket in new[] { a, b })
        {
            var chat = (await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage, Ct)).ChatMessage;
            Assert.Equal(accountA.Name, chat.SenderName);
            Assert.Equal("ping", chat.Text);
        }
    }
}
