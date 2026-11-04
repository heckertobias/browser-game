using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

/// <summary>Ein Server mit Fake-Uhr für alle Tests der Klasse.</summary>
public sealed class ChatServerFixture : IAsyncDisposable
{
    public ChatServerFixture() => Server = new GameServerFactory { TestServices = ChatServerTests.UseClock(Clock) };

    public FakeClock Clock { get; } = ChatServerTests.NewClock();

    public GameServerFactory Server { get; }

    public ValueTask DisposeAsync() => Server.DisposeAsync();
}

/// <summary>
/// 011 T12: der ganze Server über echte WebSockets, mit Fake-Uhr (SQLite, in der CI auch PostgreSQL).
/// Tests einer Klasse laufen nacheinander und teilen sich den Server; jeder hat eigene Accounts, die Uhr läuft nur vorwärts.
/// </summary>
public sealed class ChatServerTests(ChatServerFixture fixture) : IClassFixture<ChatServerFixture>
{
    private const string RejectedMarker = "#rejected";
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10); // Nur Fehlergrenze für langsame Runner.
    private static readonly TimeSpan Window = TimeSpan.FromSeconds(GameSettings.ChatRateLimitSeconds);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(GameSettings.ChatHistoryHours);
    private static readonly TimeSpan OneMs = TimeSpan.FromMilliseconds(1);

    private uint _inputSeq;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private sealed record ChatPlayer(WebSocket Socket, string Id, string Name, string Email) : IDisposable
    {
        public void Dispose() => Socket.Dispose();
    }

    // Echte Jetzt-Zeit: Was noch die echte Uhr liest, sieht keinen Widerspruch. Auf ms gekürzt wie in der Datenbank.
    internal static FakeClock NewClock() => new(DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

    internal static Action<IServiceCollection> UseClock(FakeClock clock) => services => services.AddSingleton<TimeProvider>(clock);

    private static Task<ServerMessage> ReceiveAsync(WebSocket socket, Func<ServerMessage, bool> predicate) =>
        GameSockets.ReceiveUntilAsync(socket, predicate, Limit, Ct);

    private static async Task<ChatPlayer> JoinNewPlayerAsync(GameServerFactory server)
    {
        var (socket, id, account) = await GameSockets.ConnectNewPlayerAsync(server, Ct);
        await ReceiveAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome);
        return new ChatPlayer(socket, id, account.Name, account.Email);
    }

    private static async Task<ChatMessage> ReceiveChatAsync(WebSocket socket, string text) =>
        (await ReceiveAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && m.ChatMessage.Text == text)).ChatMessage;

    private static async Task<ChatMessage> SendAndEchoAsync(ChatPlayer player, string text)
    {
        await GameSockets.SendChatAsync(player.Socket, text, Ct);
        return await ReceiveChatAsync(player.Socket, text);
    }

    /// <summary>Sammelt Chat-Nachrichten und Ablehnungen bis einschließlich <paramref name="stop"/>; Snapshots und Verlauf zählen nicht.</summary>
    private static async Task<List<string>> ReadUntilAsync(WebSocket socket, Func<ServerMessage, bool> stop)
    {
        var seen = new List<string>();
        await ReceiveAsync(socket, m =>
        {
            if (m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage)
            {
                seen.Add(m.ChatMessage.Text);
            }
            else if (m.PayloadCase == ServerMessage.PayloadOneofCase.ChatRejected)
            {
                Assert.Equal(ChatRejected.Types.Reason.TooFast, m.ChatRejected.Reason);
                seen.Add(RejectedMarker);
            }

            return stop(m);
        });
        return seen;
    }

    private static Task<List<string>> ReadUntilChatAsync(WebSocket socket, string text) =>
        ReadUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && m.ChatMessage.Text == text);

    /// <summary>
    /// Stellt die Uhr vor und wartet, bis ein Tick sie sicher gesehen hat: Die Eingabe wird im Tick vor dem Lesen der Uhr übernommen.
    /// Verwirft alles, was bis dahin ankommt; nur aufrufen, wenn der Spieler seine erwarteten Chat-Nachrichten schon gelesen hat.
    /// </summary>
    private async Task AdvanceAsync(FakeClock clock, ChatPlayer player, TimeSpan by)
    {
        clock.Advance(by);
        var seq = ++_inputSeq;
        await GameSockets.SendInputAsync(player.Socket, seq, 0, 0, Ct);
        await ReceiveAsync(player.Socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot && m.Snapshot.LastProcessedSeq >= seq);
    }

    /// <summary>Die erste Nachricht nach Welcome entscheidet ohne Warten: Verlauf oder, wenn es keinen gibt, schon der Snapshot.</summary>
    private static async Task<List<ChatMessage>> HistoryOnJoinAsync(GameServerFactory server, string cookie)
    {
        using var socket = await GameSockets.ConnectAsync(server, cookie, GameSockets.Origin, Ct);
        await ReceiveAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome);
        var next = await ReceiveAsync(socket, _ => true);
        if (next.PayloadCase == ServerMessage.PayloadOneofCase.ChatHistory)
        {
            return [.. next.ChatHistory.Messages];
        }

        Assert.Equal(ServerMessage.PayloadOneofCase.Snapshot, next.PayloadCase);
        return [];
    }

    private static async Task<List<string>> RowsAsync(GameServerFactory server, string accountId)
    {
        await using var scope = server.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return await db.ChatMessages.Where(m => m.AccountId == accountId).OrderBy(m => m.Id).Select(m => m.Text).ToListAsync(Ct);
    }

    /// <summary>Der ChatStore schreibt und räumt im echten 1-s-Takt; das Limit greift nur im Fehlerfall.</summary>
    private static async Task WaitForRowsAsync(GameServerFactory server, string accountId, params string[] expected)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(Limit);
        while (!(await RowsAsync(server, accountId)).SequenceEqual(expected))
        {
            await Task.Delay(50, timeout.Token);
        }
    }

    [Fact] // AK-2, AK-12
    public async Task Message_arrives_unchanged_with_account_name_and_server_time()
    {
        using var a = await JoinNewPlayerAsync(fixture.Server);
        using var b = await JoinNewPlayerAsync(fixture.Server);
        await AdvanceAsync(fixture.Clock, a, TimeSpan.FromHours(1));
        var expected = fixture.Clock.GetUtcNow().ToUnixTimeMilliseconds();
        const string text = "<b>fett</b> <script>alert(1)</script> https://example.org/?a=1&b=2";

        await GameSockets.SendChatAsync(a.Socket, text, Ct);

        foreach (var socket in new[] { a.Socket, b.Socket })
        {
            var message = await ReceiveChatAsync(socket, text);
            Assert.Equal((a.Name, text, expected), (message.SenderName, message.Text, message.SentAtUnixMs));
        }
    }

    [Fact] // AK-8
    public async Task Empty_or_blank_message_reaches_nobody()
    {
        using var a = await JoinNewPlayerAsync(fixture.Server);
        using var b = await JoinNewPlayerAsync(fixture.Server);

        foreach (var text in new[] { "", "   ", "\n\t ", "sentinel" })
        {
            await GameSockets.SendChatAsync(a.Socket, text, Ct);
        }

        Assert.Equal(["sentinel"], await ReadUntilChatAsync(a.Socket, "sentinel"));
        Assert.Equal(["sentinel"], await ReadUntilChatAsync(b.Socket, "sentinel"));
    }

    [Fact] // AK-10
    public async Task Message_reaches_three_players_including_sender_within_a_second()
    {
        using var a = await JoinNewPlayerAsync(fixture.Server);
        using var b = await JoinNewPlayerAsync(fixture.Server);
        using var c = await JoinNewPlayerAsync(fixture.Server);

        // Der Median fängt Bündeln im Server in jedem Lauf; ein kurzer Stau auf dem Runner trifft nur einzelne Runden.
        // Die Fake-Uhr steht still, deshalb greift das Tempolimit bei genau 5 Nachrichten nicht.
        var times = new List<TimeSpan>();
        for (var round = 0; round < GameSettings.ChatRateLimitCount; round++)
        {
            var text = $"runde-{round}";
            var started = TimeProvider.System.GetTimestamp();
            await GameSockets.SendChatAsync(a.Socket, text, Ct);
            await Task.WhenAll(ReceiveChatAsync(a.Socket, text), ReceiveChatAsync(b.Socket, text), ReceiveChatAsync(c.Socket, text));
            times.Add(TimeProvider.System.GetElapsedTime(started));
        }

        times.Sort();
        Assert.True(times[times.Count / 2] < TimeSpan.FromSeconds(1), string.Join(", ", times));
    }

    [Fact] // AK-11
    public async Task Players_writing_alternately_see_the_same_order()
    {
        using var a = await JoinNewPlayerAsync(fixture.Server);
        using var b = await JoinNewPlayerAsync(fixture.Server);

        for (var i = 0; i < GameSettings.ChatRateLimitCount; i++)
        {
            await GameSockets.SendChatAsync(a.Socket, $"a{i}", Ct);
            await GameSockets.SendChatAsync(b.Socket, $"b{i}", Ct);
        }

        var total = 2 * GameSettings.ChatRateLimitCount;
        var seenA = await ReadChatCountAsync(a.Socket, total);
        var seenB = await ReadChatCountAsync(b.Socket, total);

        Assert.Equal(seenA, seenB);
        Assert.Equal(total, seenA.Count);
        var expected = Enumerable.Range(0, GameSettings.ChatRateLimitCount);
        Assert.Equal(expected.Select(i => $"a{i}"), seenA.Where(t => t.StartsWith('a')));
        Assert.Equal(expected.Select(i => $"b{i}"), seenA.Where(t => t.StartsWith('b')));

        static Task<List<string>> ReadChatCountAsync(WebSocket socket, int count)
        {
            var chats = 0;
            return ReadUntilAsync(socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatMessage && ++chats == count);
        }
    }

    [Fact] // AK-13
    public async Task Sixth_message_is_rejected_for_sender_only_and_limit_opens_after_the_window()
    {
        using var a = await JoinNewPlayerAsync(fixture.Server);
        using var b = await JoinNewPlayerAsync(fixture.Server);
        var allowed = Enumerable.Range(0, GameSettings.ChatRateLimitCount).Select(i => $"m{i}").ToList();

        foreach (var text in allowed.Append("zu-viel"))
        {
            await GameSockets.SendChatAsync(a.Socket, text, Ct);
        }

        var seenA = await ReadUntilAsync(a.Socket, m => m.PayloadCase == ServerMessage.PayloadOneofCase.ChatRejected);
        // b hat ein eigenes Limit; ein Broadcast von "zu-viel" stünde in b's FIFO vor dem Sentinel.
        await GameSockets.SendChatAsync(b.Socket, "sentinel", Ct);
        seenA.AddRange(await ReadUntilChatAsync(a.Socket, "sentinel"));
        var seenB = await ReadUntilChatAsync(b.Socket, "sentinel");

        Assert.Equal([.. allowed, RejectedMarker, "sentinel"], seenA);
        Assert.Equal([.. allowed, "sentinel"], seenB);

        await AdvanceAsync(fixture.Clock, a, Window);
        await GameSockets.SendChatAsync(a.Socket, "wieder", Ct);

        Assert.Equal(["wieder"], await ReadUntilChatAsync(a.Socket, "wieder"));
        Assert.Equal(["wieder"], await ReadUntilChatAsync(b.Socket, "wieder"));
    }

    [Fact] // AK-15
    public async Task New_player_gets_the_last_50_of_60_messages_oldest_first()
    {
        const int total = 60;
        var perRound = GameSettings.ChatRateLimitCount;
        using var sender = await JoinNewPlayerAsync(fixture.Server);

        for (var round = 0; round < total / perRound; round++)
        {
            var texts = Enumerable.Range(round * perRound, perRound).Select(i => $"m{i}").ToList();
            foreach (var text in texts)
            {
                await GameSockets.SendChatAsync(sender.Socket, text, Ct);
            }

            await ReceiveChatAsync(sender.Socket, texts[^1]);
            await AdvanceAsync(fixture.Clock, sender, Window);
        }

        var reader = await AccountTestFlows.CreateConfirmedAccountAsync(fixture.Server);
        var history = await HistoryOnJoinAsync(fixture.Server, await GameSockets.SignInCookieAsync(fixture.Server, reader.Email));

        Assert.Equal(Enumerable.Range(total - GameSettings.ChatHistoryCount, GameSettings.ChatHistoryCount).Select(i => $"m{i}"), history.Select(m => m.Text));
        Assert.All(history, m => Assert.Equal(sender.Name, m.SenderName));
    }

    [Fact] // AK-16
    public async Task Message_older_than_24_hours_leaves_history_and_database()
    {
        using var sender = await JoinNewPlayerAsync(fixture.Server);
        await SendAndEchoAsync(sender, "alt");
        await WaitForRowsAsync(fixture.Server, sender.Id, "alt");

        fixture.Clock.Advance(TimeSpan.FromHours(25));
        var cookie = await GameSockets.SignInCookieAsync(fixture.Server, sender.Email);

        Assert.Empty(await HistoryOnJoinAsync(fixture.Server, cookie));
        await WaitForRowsAsync(fixture.Server, sender.Id);
    }

    [Fact] // AK-16, AK-15, AK-12
    public async Task Restart_on_the_24_hour_boundary_serves_and_cleans_only_what_is_expired()
    {
        const string young = "<b>jung</b> https://example.org/?a=1&b=2";
        var clock = NewClock();
        await using var first = new GameServerFactory { KeepDatabase = true, TestServices = UseClock(clock) };
        await using var second = new GameServerFactory { ConnectionString = first.ConnectionString, TestServices = UseClock(clock) };
        using var sender = await JoinNewPlayerAsync(first);
        var t0 = clock.GetUtcNow();

        Assert.Equal(t0.ToUnixTimeMilliseconds(), (await SendAndEchoAsync(sender, "alt")).SentAtUnixMs);
        await AdvanceAsync(clock, sender, OneMs);
        Assert.Equal((t0 + OneMs).ToUnixTimeMilliseconds(), (await SendAndEchoAsync(sender, "genau")).SentAtUnixMs);
        await AdvanceAsync(clock, sender, OneMs);
        Assert.Equal((t0 + 2 * OneMs).ToUnixTimeMilliseconds(), (await SendAndEchoAsync(sender, young)).SentAtUnixMs);

        await first.DisposeAsync();
        clock.Advance(MaxAge - OneMs);
        var cookie = await GameSockets.SignInCookieAsync(second, sender.Email);

        var history = await HistoryOnJoinAsync(second, cookie);
        var only = Assert.Single(history);
        Assert.Equal((sender.Name, young, (t0 + 2 * OneMs).ToUnixTimeMilliseconds()), (only.SenderName, only.Text, only.SentAtUnixMs));
        await WaitForRowsAsync(second, sender.Id, "genau", young);

        clock.Advance(OneMs);
        Assert.Empty(await HistoryOnJoinAsync(second, cookie));
        Assert.Equal(["genau", young], await RowsAsync(second, sender.Id));

        clock.Advance(TimeSpan.FromHours(GameSettings.ChatCleanupIntervalHours));
        await WaitForRowsAsync(second, sender.Id);
    }
}
