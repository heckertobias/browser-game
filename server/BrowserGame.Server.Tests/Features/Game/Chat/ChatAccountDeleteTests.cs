using System.Net;
using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace BrowserGame.Server.Tests;

/// <summary>011 T7: echte Delete-Seite, echter WebSocket, echte Datenbank (SQLite, in der CI auch PostgreSQL).</summary>
public sealed class ChatAccountDeleteTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static Task<(HttpResponseMessage Response, string Html)> DeleteAsync(FormClient client) =>
        client.PostAsync("/Account/Delete", "/Account/Delete", new Dictionary<string, string> { ["Password"] = AccountTestFlows.Password });

    /// <summary>Spieler mit eigener Anmeldung fürs Formular und offenem Socket; Welcome ist schon gelesen.</summary>
    private static async Task<(FormClient Client, WebSocket Socket, string Id)> PlayerWithFormAsync(GameServerFactory server)
    {
        var (client, account) = await AccountTestFlows.SignInAsync(server);
        var socket = await GameSockets.ConnectAsync(server, await GameSockets.SignInCookieAsync(server, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        return (client, socket, await GameSockets.AccountIdAsync(server, account.Email));
    }

    private static async Task<(WebSocket Socket, string Id)> OtherPlayerAsync(GameServerFactory server)
    {
        var (socket, id, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        return (socket, id);
    }

    private static async Task<List<string>> RowsAsync(GameServerFactory server, string accountId)
    {
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return await db.ChatMessages.Where(m => m.AccountId == accountId).OrderBy(m => m.Id).Select(m => m.Text).ToListAsync(Ct);
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    private static Task<ServerMessage> ReceiveChatAsync(WebSocket socket, string text) =>
        GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && m.ChatMessage.Text == text, Ct);

    /// <summary>Das Erste nach Welcome außer Snapshots MUSS die ChatHistory sein.</summary>
    private static async Task<List<string>> HistoryOfNewPlayerAsync(GameServerFactory server)
    {
        var (socket, _, _) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        var next = await GameSockets.ReceiveUntilAsync(socket, m => m.PayloadCase != ServerMessage.PayloadOneofCase.Snapshot, Ct);
        Assert.Equal(ServerMessage.PayloadOneofCase.ChatHistory, next.PayloadCase);
        return [.. next.ChatHistory.Messages.Select(m => m.Text)];
    }

    [Fact] // AK-17
    public async Task Delete_page_removes_written_messages_from_history_and_database_and_keeps_others()
    {
        await using var server = new GameServerFactory();
        var (other, otherId) = await OtherPlayerAsync(server);
        var (client, doomed, id) = await PlayerWithFormAsync(server);
        using var _ = client;
        await GameSockets.SendChatAsync(doomed, "weg-1", Ct);
        await GameSockets.SendChatAsync(doomed, "weg-2", Ct);
        await GameSockets.SendChatAsync(other, "bleibt", Ct);
        await WaitUntilAsync(async () => (await RowsAsync(server, id)).Count == 2 && (await RowsAsync(server, otherId)).Count == 1);

        var (response, _) = await DeleteAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var kicked = await GameSockets.ReceiveUntilAsync(doomed, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Kicked, Ct);
        Assert.Equal(Kicked.Types.Reason.SignedOut, kicked.Kicked.Reason);
        Assert.Empty(await RowsAsync(server, id));
        Assert.Equal(["bleibt"], await RowsAsync(server, otherId));
        Assert.Equal(["bleibt"], await HistoryOfNewPlayerAsync(server));
    }

    [Fact] // AK-17
    public async Task Delete_page_loses_messages_still_waiting_to_be_written()
    {
        await using var server = new GameServerFactory();
        var (other, otherId) = await OtherPlayerAsync(server);
        var (client, doomed, id) = await PlayerWithFormAsync(server);
        using var _ = client;
        await GameSockets.SendChatAsync(doomed, "weg", Ct);
        await ReceiveChatAsync(other, "weg");

        var (response, _) = await DeleteAsync(client);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await GameSockets.SendChatAsync(other, "danach", Ct);
        await WaitUntilAsync(async () => (await RowsAsync(server, otherId)).Contains("danach"));

        Assert.Empty(await RowsAsync(server, id));
        Assert.Equal(["danach"], await HistoryOfNewPlayerAsync(server));
    }

    [Fact] // AK-17
    public async Task Failed_delete_keeps_connection_and_messages()
    {
        await using var server = new GameServerFactory { TestServices = s => s.AddScoped<UserManager<Account>, FailingDeleteUserManager>() };
        var (client, player, id) = await PlayerWithFormAsync(server);
        using var _ = client;
        await GameSockets.SendChatAsync(player, "vorher", Ct);
        await WaitUntilAsync(async () => (await RowsAsync(server, id)).Count == 1);

        var (response, _) = await DeleteAsync(client);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(["vorher"], await RowsAsync(server, id));
        await GameSockets.SendChatAsync(player, "danach", Ct);
        var message = await GameSockets.ReceiveUntilAsync(player, m =>
            m.PayloadCase == ServerMessage.PayloadOneofCase.Kicked
            || (m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && m.ChatMessage.Text == "danach"), Ct);
        Assert.Equal(ServerMessage.PayloadOneofCase.ChatMessage, message.PayloadCase);
        await WaitUntilAsync(async () => (await RowsAsync(server, id)).Count == 2);
        Assert.Equal(["vorher", "danach"], await HistoryOfNewPlayerAsync(server));
    }
}

/// <summary>Löschen scheitert immer, alles andere läuft normal.</summary>
public sealed class FailingDeleteUserManager(
    IUserStore<Account> store, IOptions<IdentityOptions> options, IPasswordHasher<Account> hasher,
    IEnumerable<IUserValidator<Account>> userValidators, IEnumerable<IPasswordValidator<Account>> passwordValidators,
    ILookupNormalizer normalizer, IdentityErrorDescriber errors, IServiceProvider services, ILogger<UserManager<Account>> logger)
    : UserManager<Account>(store, options, hasher, userValidators, passwordValidators, normalizer, errors, services, logger)
{
    public override Task<IdentityResult> DeleteAsync(Account user) => Task.FromResult(IdentityResult.Failed());
}
