using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>011 T6: der echte ChatStore gegen die echte Datenbank (SQLite, in der CI auch PostgreSQL), mit Fake-Uhr.</summary>
public sealed class ChatStoreTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Start = new(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(GameSettings.ChatHistoryHours);
    private static readonly TimeSpan OneMs = TimeSpan.FromMilliseconds(1);

    private readonly string _connectionString = TestDatabase.NewConnectionString("bg-chatstore");
    private readonly FakeClock _clock = new(Start);
    private ServiceProvider _services = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = GameServerFactory.Provider,
            ["ConnectionStrings:Game"] = _connectionString,
        }).Build();

        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<TimeProvider>(_clock);
        var provider = services.AddGameDatabase(configuration);
        _services = services.BuildServiceProvider();
        await _services.MigrateAsync<GameDbContext>(provider);

        await WithDbAsync(async db =>
        {
            db.Users.Add(new Account { Id = "a", UserName = "Anna", NormalizedUserName = "ANNA" });
            db.Users.Add(new Account { Id = "b", UserName = "Ben", NormalizedUserName = "BEN" });
            await db.SaveChangesAsync(Ct);
        });
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        TestDatabase.Drop(_connectionString);
    }

    private async Task WithDbAsync(Func<GameDbContext, Task> action)
    {
        await using var scope = _services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<GameDbContext>());
    }

    private GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build(), _clock);

    private ChatStore NewStore(
        GameLoop game, int saveBatchSize = GameSettings.ChatSaveBatchSize, int cleanupBatchSize = GameSettings.ChatCleanupBatchSize,
        int saveIntervalSeconds = GameSettings.ChatSaveIntervalSeconds) => new(
        game, _services.GetRequiredService<IServiceScopeFactory>(), _clock, NullLogger<ChatStore>.Instance, saveBatchSize, cleanupBatchSize, saveIntervalSeconds);

    private static ChatHistoryEntry Entry(string text, DateTimeOffset sentAt, string account = "a") => new(account, "", text, sentAt);

    private async Task SeedAsync(params ChatMessageEntry[] rows) => await WithDbAsync(async db =>
    {
        foreach (var row in rows)
        {
            db.ChatMessages.Add(row);
            await db.SaveChangesAsync(Ct);
        }
    });

    private static ChatMessageEntry Row(string text, DateTimeOffset sentAt, string account = "a") => new() { AccountId = account, Text = text, SentAt = sentAt };

    /// <summary>Alle Texte in Id-Reihenfolge, also so, wie sie geschrieben wurden.</summary>
    private async Task<List<string>> TextsByIdAsync()
    {
        List<string> texts = [];
        await WithDbAsync(async db => texts = await db.ChatMessages.OrderBy(m => m.Id).Select(m => m.Text).ToListAsync(Ct));
        return texts;
    }

    // Echter Datenbankfehler auf beiden Providern, ohne den Store zu verändern.
    private Task BreakTableAsync() => WithDbAsync(db => db.Database.ExecuteSqlRawAsync("""ALTER TABLE "ChatMessages" RENAME TO "ChatMessagesBroken" """, Ct));

    private Task RepairTableAsync() => WithDbAsync(db => db.Database.ExecuteSqlRawAsync("""ALTER TABLE "ChatMessagesBroken" RENAME TO "ChatMessages" """, Ct));

    private Task DeleteAccountAsync(string id) => WithDbAsync(db => db.Users.Where(u => u.Id == id).ExecuteDeleteAsync(Ct));

    private static async Task WaitUntilAsync(Func<Task<bool>> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        while (!await condition())
        {
            await Task.Delay(20, timeout.Token);
        }
    }

    [Fact] // AK-15
    public async Task LoadRecent_returns_the_newest_50_of_60_oldest_first_with_sender_name()
    {
        using var game = NewGame();
        await SeedAsync([.. Enumerable.Range(0, 60).Select(i => Row($"m{i}", Start.AddMinutes(i - 60)))]);

        var recent = await NewStore(game).LoadRecentAsync(Start, Ct);

        Assert.Equal(Enumerable.Range(10, 50).Select(i => $"m{i}"), recent.Select(m => m.Text));
        Assert.All(recent, m => Assert.Equal(("a", "Anna"), (m.AccountId, m.SenderName)));
        Assert.Equal(Start.AddMinutes(-50), recent[0].SentAt);
    }

    [Fact] // AK-15, AK-16
    public async Task LoadRecent_excludes_exactly_24_hours_and_includes_one_ms_younger()
    {
        using var game = NewGame();
        await SeedAsync(Row("zu-alt", Start - MaxAge - OneMs), Row("genau", Start - MaxAge), Row("jung", Start - MaxAge + OneMs));

        var recent = await NewStore(game).LoadRecentAsync(Start, Ct);

        Assert.Equal(["jung"], recent.Select(m => m.Text));
    }

    [Fact] // AK-16
    public async Task DeleteExpired_deletes_only_older_than_24_hours()
    {
        using var game = NewGame();
        await SeedAsync(Row("zu-alt", Start - MaxAge - OneMs), Row("genau", Start - MaxAge), Row("jung", Start - MaxAge + OneMs));

        Assert.Equal(1, await NewStore(game).DeleteExpiredAsync(Start, GameSettings.ChatCleanupBatchSize, Ct));

        Assert.Equal(["genau", "jung"], await TextsByIdAsync());
    }

    [Fact] // AK-16
    public async Task DeleteExpired_respects_the_batch_size_oldest_first()
    {
        using var game = NewGame();
        await SeedAsync(Row("alt3", Start.AddHours(-25)), Row("alt1", Start.AddHours(-27)), Row("alt2", Start.AddHours(-26)));

        Assert.Equal(2, await NewStore(game).DeleteExpiredAsync(Start, 2, Ct));

        Assert.Equal(["alt3"], await TextsByIdAsync());
    }

    [Fact] // AK-16
    public async Task Cleanup_runs_in_the_first_cycle_and_then_hourly()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await SeedAsync(Row("alt", Start - MaxAge - OneMs));

        await store.RunCycleAsync(Ct);
        Assert.Empty(await TextsByIdAsync());

        // Ist nach 30 Minuten abgelaufen, wird aber erst beim nächsten vollen Stundenlauf gelöscht.
        await SeedAsync(Row("spaeter", Start - MaxAge + TimeSpan.FromMinutes(30)));
        _clock.Advance(TimeSpan.FromHours(1) - OneMs);
        await store.RunCycleAsync(Ct);
        Assert.Equal(["spaeter"], await TextsByIdAsync());

        _clock.Advance(OneMs);
        await store.RunCycleAsync(Ct);
        Assert.Empty(await TextsByIdAsync());
    }

    [Fact] // AK-16
    public async Task Big_cleanup_takes_one_batch_per_cycle_until_nothing_is_left()
    {
        using var game = NewGame();
        var store = NewStore(game, cleanupBatchSize: 2);
        await SeedAsync([.. Enumerable.Range(0, 5).Select(i => Row($"alt{i}", Start.AddHours(-25)))]);

        await store.RunCycleAsync(Ct);
        Assert.Equal(3, (await TextsByIdAsync()).Count);
        await store.RunCycleAsync(Ct);
        Assert.Single(await TextsByIdAsync());
        await store.RunCycleAsync(Ct);
        Assert.Empty(await TextsByIdAsync());

        // Der letzte Stapel war nicht voll: Fertig für diese Stunde.
        await SeedAsync(Row("neu-alt", Start.AddHours(-25)));
        await store.RunCycleAsync(Ct);
        Assert.Single(await TextsByIdAsync());
    }

    [Fact] // AK-15, AK-11
    public async Task Cycle_writes_messages_in_acceptance_order_across_batches_with_the_same_time()
    {
        using var game = NewGame();
        var store = NewStore(game, saveBatchSize: 3);
        var texts = Enumerable.Range(0, 40).Select(i => $"m{i}").ToArray();
        for (var i = 0; i < texts.Length; i++)
        {
            game.ChatToSave.Add(Entry(texts[i], Start, i % 2 == 0 ? "a" : "b"));
        }

        await store.RunCycleAsync(Ct);

        Assert.Equal(texts, await TextsByIdAsync());
        Assert.Equal(texts, (await store.LoadRecentAsync(Start, Ct)).Select(m => m.Text));
    }

    [Fact] // AK-15
    public async Task Accepted_message_reaches_the_database_with_account_and_time()
    {
        using var game = NewGame();
        var store = NewStore(game);
        var connection = game.Join("a", "Anna", null);
        Assert.True(connection.Chat.Writer.TryWrite("  hallo  "));
        Assert.True(connection.Chat.Writer.TryWrite(new string('x', GameSettings.ChatMaxLength + 1)));
        game.Tick();

        await store.RunCycleAsync(Ct);

        ChatMessageEntry row = null!;
        await WithDbAsync(async db => row = await db.ChatMessages.SingleAsync(Ct));
        Assert.Equal(("a", "hallo", Start), (row.AccountId, row.Text, row.SentAt));
    }

    [Fact] // AK-15
    public async Task Save_keeps_text_with_markup_and_emoji_unchanged()
    {
        using var game = NewGame();
        const string text = "<b>🙂 'x' \"y\" ; DROP TABLE";

        await NewStore(game).SaveAsync([Entry(text, Start)], Ct);

        Assert.Equal([text], await TextsByIdAsync());
    }

    [Fact] // AK-17
    public async Task Save_skips_deleted_accounts_and_keeps_the_rest()
    {
        using var game = NewGame();
        await DeleteAccountAsync("b");

        await NewStore(game).SaveAsync([Entry("a1", Start), Entry("weg", Start, "b"), Entry("a2", Start)], Ct);

        Assert.Equal(["a1", "a2"], await TextsByIdAsync());
    }

    [Fact] // AK-17, AK-15
    public async Task Failed_batch_is_retried_in_order_and_loses_only_the_deleted_account()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("a1", Start));
        game.ChatToSave.Add(Entry("weg", Start, "b"));
        game.ChatToSave.Add(Entry("a2", Start));

        await store.RunCycleAsync(Ct);

        await RepairTableAsync();
        await DeleteAccountAsync("b");
        game.ChatToSave.Add(Entry("a3", Start));
        await store.RunCycleAsync(Ct);

        Assert.Equal(["a1", "a2", "a3"], await TextsByIdAsync());
    }

    [Fact] // AK-17, AK-15
    public async Task Postgres_foreign_key_race_loses_only_the_deleted_account_on_retry()
    {
        Assert.SkipUnless(TestDatabase.IsPostgres, "nur PostgreSQL");
        using var game = NewGame();
        var store = NewStore(game);
        game.ChatToSave.Add(Entry("a1", Start));
        game.ChatToSave.Add(Entry("weg", Start, "b"));
        game.ChatToSave.Add(Entry("a2", Start));

        await using var scope = _services.CreateAsyncScope();
        var other = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await using var transaction = await other.Database.BeginTransactionAsync(Ct);
        await other.Users.Where(u => u.Id == "b").ExecuteDeleteAsync(Ct);

        var cycle = Task.Run(() => store.RunCycleAsync(Ct), Ct);
        await WaitUntilBlockedAsync();
        await transaction.CommitAsync(Ct);
        await cycle;
        Assert.Empty(await TextsByIdAsync());

        game.ChatToSave.Add(Entry("a3", Start));
        await store.RunCycleAsync(Ct);

        Assert.Equal(["a1", "a2", "a3"], await TextsByIdAsync());
    }

    [Fact] // AK-17, AK-15
    public async Task Postgres_foreign_key_race_during_stop_is_retried_once()
    {
        Assert.SkipUnless(TestDatabase.IsPostgres, "nur PostgreSQL");
        using var game = NewGame();
        var store = NewStore(game, saveIntervalSeconds: 3600);
        await SeedAsync(Row("alt", Start - MaxAge - OneMs));
        await store.StartAsync(Ct);
        // Das Aufräumen gehört zum ersten Durchlauf: Ist die Zeile weg, ist er durch.
        await WaitUntilAsync(async () => (await TextsByIdAsync()).Count == 0);

        game.ChatToSave.Add(Entry("a1", Start));
        game.ChatToSave.Add(Entry("weg", Start, "b"));
        game.ChatToSave.Add(Entry("a2", Start));
        await using var scope = _services.CreateAsyncScope();
        var other = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await using var transaction = await other.Database.BeginTransactionAsync(Ct);
        await other.Users.Where(u => u.Id == "b").ExecuteDeleteAsync(Ct);

        var stop = store.StopAsync(Ct);
        await WaitUntilBlockedAsync();
        await transaction.CommitAsync(Ct);
        await stop;

        Assert.Equal(["a1", "a2"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Success_resets_the_failure_clock()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("m1", Start));
        await store.RunCycleAsync(Ct);
        await RepairTableAsync();
        await store.RunCycleAsync(Ct);

        _clock.Advance(TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes));
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("m2", _clock.GetUtcNow()));
        await store.RunCycleAsync(Ct);
        await RepairTableAsync();
        await store.RunCycleAsync(Ct);

        Assert.Equal(["m1", "m2"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Giving_up_resets_the_failure_clock()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("x", Start));
        await store.RunCycleAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes));
        await store.RunCycleAsync(Ct);

        game.ChatToSave.Add(Entry("y", _clock.GetUtcNow()));
        await store.RunCycleAsync(Ct);
        await RepairTableAsync();
        await store.RunCycleAsync(Ct);

        Assert.Equal(["y"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Giving_up_drops_only_one_batch()
    {
        using var game = NewGame();
        var store = NewStore(game, saveBatchSize: 2);
        await BreakTableAsync();
        for (var i = 0; i < 5; i++)
        {
            game.ChatToSave.Add(Entry($"m{i}", Start));
        }

        await store.RunCycleAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes));
        await store.RunCycleAsync(Ct);
        await RepairTableAsync();
        await store.RunCycleAsync(Ct);

        Assert.Equal(["m2", "m3", "m4"], await TextsByIdAsync());
    }

    private async Task WaitUntilBlockedAsync() => await WaitUntilAsync(async () =>
    {
        var waiting = 0;
        await WithDbAsync(async db => waiting = await db.Database.SqlQueryRaw<int>(
            "SELECT count(*)::int AS \"Value\" FROM pg_stat_activity WHERE datname = current_database() AND wait_event_type = 'Lock'").SingleAsync(Ct));
        return waiting > 0;
    });

    [Fact] // AK-15
    public async Task Batch_is_given_up_after_the_limit_and_later_messages_still_arrive()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("verloren", Start));

        await store.RunCycleAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes));
        await store.RunCycleAsync(Ct);

        await RepairTableAsync();
        game.ChatToSave.Add(Entry("danach", _clock.GetUtcNow()));
        await store.RunCycleAsync(Ct);

        Assert.Equal(["danach"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Batch_within_the_limit_is_not_given_up()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();
        game.ChatToSave.Add(Entry("bleibt", Start));

        await store.RunCycleAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes) - OneMs);
        await store.RunCycleAsync(Ct);

        await RepairTableAsync();
        await store.RunCycleAsync(Ct);

        Assert.Equal(["bleibt"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Save_waits_for_another_writer_instead_of_losing_the_batch()
    {
        using var game = NewGame();
        var store = NewStore(game);
        game.ChatToSave.Add(Entry("meins", Start));

        await using var scope = _services.CreateAsyncScope();
        var other = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await using var transaction = await other.Database.BeginTransactionAsync(Ct);
        other.ChatMessages.Add(Row("anderer", Start));
        await other.SaveChangesAsync(Ct);

        // Task.Run: Microsoft.Data.Sqlite wartet synchron, der Testfaden muss frei bleiben, um zu committen.
        var cycle = Task.Run(() => store.RunCycleAsync(Ct), Ct);
        await Task.Delay(200, Ct);
        if (!TestDatabase.IsPostgres)
        {
            Assert.False(cycle.IsCompleted); // SQLite: ein Schreiber je Datei
        }

        await transaction.CommitAsync(Ct);
        await cycle;

        Assert.Equal(["anderer", "meins"], (await TextsByIdAsync()).Order(StringComparer.Ordinal));
    }

    [Fact] // AK-16
    public async Task Cleanup_failure_does_not_stop_the_cycle_and_waits_for_the_next_hour()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await SeedAsync(Row("alt", Start.AddHours(-25)));
        await BreakTableAsync();

        await store.RunCycleAsync(Ct);

        await RepairTableAsync();
        await store.RunCycleAsync(Ct);
        Assert.Equal(["alt"], await TextsByIdAsync());

        _clock.Advance(TimeSpan.FromHours(1));
        await store.RunCycleAsync(Ct);
        Assert.Empty(await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Service_survives_database_errors_and_writes_after_recovery()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();

        await store.StartAsync(Ct);
        game.ChatToSave.Add(Entry("kommt-an", Start));
        await Task.Delay(TimeSpan.FromSeconds(GameSettings.ChatSaveIntervalSeconds) * 1.5, Ct);
        Assert.False(store.ExecuteTask!.IsCompleted);

        await RepairTableAsync();
        await WaitUntilAsync(async () => (await TextsByIdAsync()).Count == 1);

        await store.StopAsync(Ct);
        Assert.True(store.ExecuteTask.IsCompletedSuccessfully);
        Assert.Equal(["kommt-an"], await TextsByIdAsync());
    }

    [Fact] // AK-15
    public async Task Failed_load_does_not_stop_the_start()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await BreakTableAsync();

        await store.StartAsync(Ct);
        await store.StopAsync(Ct);

        var connection = game.Join("a", "Anna", null);
        game.Tick();
        Assert.True(connection.Outbox.Reader.TryRead(out var first));
        Assert.Equal(ServerMessage.PayloadOneofCase.Welcome, ServerMessage.Parser.ParseFrom(first).PayloadCase);
        Assert.True(connection.Outbox.Reader.TryRead(out var second));
        Assert.Equal(ServerMessage.PayloadOneofCase.Snapshot, ServerMessage.Parser.ParseFrom(second).PayloadCase);
    }

    [Fact] // AK-15, AK-16
    public async Task Start_loads_the_history_before_the_first_join_and_filters_age()
    {
        using var game = NewGame();
        var store = NewStore(game);
        await SeedAsync(Row("alt", Start - MaxAge), Row("eins", Start.AddHours(-2)), Row("zwei", Start.AddHours(-1), "b"));

        await store.StartAsync(Ct);
        await store.StopAsync(Ct);
        var connection = game.Join("a", "Anna", null);
        game.Tick();

        Assert.True(connection.Outbox.Reader.TryRead(out _));
        Assert.True(connection.Outbox.Reader.TryRead(out var second));
        var history = ServerMessage.Parser.ParseFrom(second).ChatHistory;
        Assert.Equal(["eins", "zwei"], history.Messages.Select(m => m.Text));
        Assert.Equal(["Anna", "Ben"], history.Messages.Select(m => m.SenderName));
        Assert.Equal(Start.AddHours(-2).ToUnixTimeMilliseconds(), history.Messages[0].SentAtUnixMs);
    }

    [Fact] // AK-15
    public async Task Stop_writes_what_is_still_waiting()
    {
        using var game = NewGame();
        var store = NewStore(game, saveIntervalSeconds: 3600);
        var connection = game.Join("a", "Anna", null);
        await store.StartAsync(Ct);
        // Der erste Durchlauf ist durch; der nächste käme erst in einer Sekunde, also schreibt nur das Stoppen.
        await Task.Delay(100, Ct);

        Assert.True(connection.Chat.Writer.TryWrite("letzter-tick"));
        game.Tick();
        await store.StopAsync(Ct);

        Assert.Equal(["letzter-tick"], await TextsByIdAsync());
    }

    [Fact] // AK-15, AK-16
    public void Chat_store_starts_before_and_stops_after_the_game_loop()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = GameServerFactory.Provider,
            ["ConnectionStrings:Game"] = _connectionString,
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddGameDatabase(configuration);
        services.AddGame();
        using var provider = services.BuildServiceProvider();

        var hosted = provider.GetServices<IHostedService>().ToList();

        var store = hosted.FindIndex(s => s is ChatStore);
        Assert.InRange(store, 0, hosted.FindIndex(s => s is GameLoop) - 1);
    }
}
