using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

/// <summary>015 T4: Slash-Commands über echte WebSockets (SQLite, in der CI auch PostgreSQL).</summary>
public sealed class GameCommandSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string Sentinel = "sentinel";
    private const float MaxHp = (float)GameSettings.MaxHp;
    private const float MaxMana = (float)GameSettings.MaxMana;
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10); // Nur Fehlergrenze für langsame Runner.

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static GameLoop Game(GameServerFactory server) => server.Services.GetRequiredService<GameLoop>();

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

    private async Task<Joined> JoinAtAsync(double x, double y)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        await GameSockets.SeedPositionAsync(factory, id, x, y);
        var socket = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        await GameSockets.ReceiveWelcomeAsync(socket, Ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.Players.Any(p => p.Id == id), Ct);
        return new Joined(socket, id, account.Name);
    }

    // Pflichtnachrichten kommen FIFO; ein Echo oder eine Ablehnung des Commands stünde vor dem Sentinel.
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

    private static async Task<List<string>> RowsAsync(GameServerFactory server, string accountId)
    {
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return await db.ChatMessages.Where(m => m.AccountId == accountId).OrderBy(m => m.Id).Select(m => m.Text).ToListAsync(Ct);
    }

    /// <summary>Der ChatStore schreibt im echten 1-s-Takt; das Limit greift nur im Fehlerfall.</summary>
    private static async Task WaitForRowsAsync(GameServerFactory server, string accountId, params string[] expected)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(Limit);
        while (!(await RowsAsync(server, accountId)).SequenceEqual(expected))
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact] // AK-1, AK-2, AK-5, AK-6, AK-9
    public async Task Command_is_not_distributed_heals_the_sender_and_stays_out_of_history_and_database()
    {
        using var a = await JoinAsync();
        using var b = await JoinAsync();
        Game(factory).SetVitals(a.Id, hp: 30, mana: 0);
        await GameSockets.ReceiveSnapshotUntilAsync(a.Socket, s => s.Hp == 30f, Ct);

        await GameSockets.SendChatAsync(a.Socket, "/run fullHealth()", Ct);
        await GameSockets.SendChatAsync(a.Socket, Sentinel, Ct);

        Assert.Equal([Sentinel], await ReadUntilSentinelAsync(a.Socket, a.Name));
        Assert.Equal([Sentinel], await ReadUntilSentinelAsync(b.Socket, a.Name));
        var healed = await GameSockets.ReceiveSnapshotUntilAsync(a.Socket, _ => true, Ct);
        Assert.Equal(MaxHp, healed.Hp);
        Assert.Equal(MaxMana, healed.Mana);
        await WaitForRowsAsync(factory, a.Id, Sentinel);

        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        using var reader = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, account.Email), GameSockets.Origin, Ct);
        var history = (await GameSockets.ReceiveUntilAsync(reader, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory, Limit, Ct)).ChatHistory;
        Assert.Equal([Sentinel], history.Messages.Where(m => m.SenderName == a.Name).Select(m => m.Text));
    }

    [Fact] // AK-4
    public async Task Unknown_or_invalid_command_is_rejected_for_sender_only_without_effect()
    {
        using var a = await JoinAsync();
        using var b = await JoinAsync();
        Game(factory).SetVitals(a.Id, hp: 30);
        await GameSockets.ReceiveSnapshotUntilAsync(a.Socket, s => s.Hp == 30f, Ct);

        (string Text, CommandRejected.Types.Reason Reason)[] cases =
        [
            ("/xyz", CommandRejected.Types.Reason.UnknownCommand),
            ("/run nope()", CommandRejected.Types.Reason.UnknownCommand),
            ("/run fullHealth(1)", CommandRejected.Types.Reason.Invalid),
        ];
        foreach (var (text, reason) in cases)
        {
            await GameSockets.SendChatAsync(a.Socket, text, Ct);
            var answer = await GameSockets.ReceiveUntilAsync(a.Socket, m => m.PayloadCase is
                ServerMessage.PayloadOneofCase.CommandRejected or ServerMessage.PayloadOneofCase.ChatMessage or ServerMessage.PayloadOneofCase.ChatRejected, Limit, Ct);
            Assert.Equal(ServerMessage.PayloadOneofCase.CommandRejected, answer.PayloadCase);
            Assert.Equal(reason, answer.CommandRejected.Reason);
            var snapshot = await GameSockets.ReceiveSnapshotUntilAsync(a.Socket, _ => true, Ct);
            Assert.Equal(30f, snapshot.Hp);
            Assert.Equal(0u, snapshot.Players.Single(p => p.Id == a.Id).SpawnSeq);
        }

        await GameSockets.SendChatAsync(a.Socket, Sentinel, Ct);
        Assert.Equal([Sentinel], await ReadUntilSentinelAsync(b.Socket, a.Name));
    }

    [Fact] // AK-8, AK-9
    public async Task Other_player_sees_reset_position_within_a_second()
    {
        using var a = await JoinAtAsync(500, 500);
        using var b = await JoinAsync();
        await GameSockets.ReceiveSnapshotUntilAsync(b.Socket, s => s.Players.Any(p => p.Id == a.Id && p.X > 400), Ct);

        await GameSockets.SendChatAsync(a.Socket, "/run resetPosition()", Ct);
        var startedAt = TimeProvider.System.GetTimestamp();
        var seen = await GameSockets.ReceiveSnapshotUntilAsync(b.Socket, s => s.Players.Any(p => p.Id == a.Id && p.SpawnSeq == 1), Ct);

        Assert.True(TimeProvider.System.GetElapsedTime(startedAt) < TimeSpan.FromSeconds(1));
        var moved = seen.Players.Single(p => p.Id == a.Id);
        Assert.True(Math.Sqrt((double)moved.X * moved.X + (double)moved.Y * moved.Y) <= World.SpawnRadius + 0.01);
    }
}
