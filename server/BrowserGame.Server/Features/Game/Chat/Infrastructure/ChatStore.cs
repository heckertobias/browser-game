using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Datenbank des Chats (011 AK-15, AK-16): lädt beim Start den Verlauf in den <see cref="GameLoop"/>, schreibt angenommene
/// Nachrichten gebündelt und löscht abgelaufene. Vor dem GameLoop registrieren: So ist der Verlauf vor dem ersten Tick
/// da, und beim Herunterfahren stoppt der Store erst nach dem letzten Tick.
/// </summary>
internal sealed partial class ChatStore(
    GameLoop game, IServiceScopeFactory scopes, TimeProvider time, ILogger<ChatStore> logger,
    int saveBatchSize = GameSettings.ChatSaveBatchSize, int cleanupBatchSize = GameSettings.ChatCleanupBatchSize,
    int saveIntervalSeconds = GameSettings.ChatSaveIntervalSeconds) : BackgroundService
{
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(GameSettings.ChatHistoryHours);
    private static readonly TimeSpan CleanupInterval = TimeSpan.FromHours(GameSettings.ChatCleanupIntervalHours);
    private static readonly TimeSpan GiveUpAfter = TimeSpan.FromMinutes(GameSettings.ChatSaveGiveUpMinutes);

    // Nur die Schleife des Dienstes greift darauf zu.
    private readonly List<ChatHistoryEntry> _pending = [];
    private DateTimeOffset? _failingSince;
    private DateTimeOffset _nextCleanup = DateTimeOffset.MinValue;

    /// <summary>Läuft, bevor der Server Verbindungen annimmt und bevor der GameLoop tickt: Kein Spieler sieht einen leeren Verlauf.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            game.LoadChatHistory(await LoadRecentAsync(time.GetUtcNow(), cancellationToken));
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // Der Chat läuft ohne alten Verlauf weiter; beim nächsten Neustart ist er wieder da.
            LogLoadFailed(ex);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(saveIntervalSeconds));
        try
        {
            do
            {
                await RunCycleAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Herunterfahren: Das Ausstehende folgt unten.
        }

        // Der GameLoop ist schon gestoppt (siehe GameSetup), hier kommt nichts mehr nach. Der zweite Versuch trifft einen
        // Account, der beim ersten gerade gelöscht wurde, nicht mehr an.
        if (game.ChatToSave.TakeDroppedCount() is > 0 and var dropped)
        {
            LogDropped(dropped);
        }

        if (!await SavePendingAsync() && !await SavePendingAsync())
        {
            var lost = _pending.Count;
            while (game.ChatToSave.TryRead(out _))
            {
                lost++;
            }

            LogShutdownLost(lost);
        }
    }

    /// <summary>Ein Durchlauf: alles Wartende schreiben, dann höchstens einen Löschstapel. Fängt jeden Datenbankfehler ab.</summary>
    internal async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        if (game.ChatToSave.TakeDroppedCount() is > 0 and var dropped)
        {
            LogDropped(dropped);
        }

        if (await SavePendingAsync())
        {
            await CleanupIfDueAsync(stoppingToken);
        }
    }

    private async Task<bool> SavePendingAsync()
    {
        while (true)
        {
            while (_pending.Count < saveBatchSize && game.ChatToSave.TryRead(out var entry))
            {
                _pending.Add(entry);
            }

            if (_pending.Count == 0)
            {
                return true;
            }

            try
            {
                // Nicht abbrechbar: Auch beim Herunterfahren soll der Stapel in die Datenbank.
                await SaveAsync(_pending, CancellationToken.None);
                _pending.Clear();
                _failingSince = null;
            }
            catch (Exception ex)
            {
                HandleSaveFailure(ex);
                return false;
            }
        }
    }

    /// <summary>
    /// Der Stapel bleibt und kommt im nächsten Durchlauf wieder dran; so bleibt die Reihenfolge. War ein Account gelöscht,
    /// fehlen beim nächsten Versuch nur seine Nachrichten. Geht es länger nicht, fällt der Stapel weg, damit eine Nachricht,
    /// die nie geschrieben werden kann, nicht alle späteren aufhält.
    /// </summary>
    private void HandleSaveFailure(Exception exception)
    {
        var now = time.GetUtcNow();
        if (_failingSince is not { } since)
        {
            _failingSince = now;
            LogSaveFailed(exception, _pending.Count);
        }
        else if (now - since >= GiveUpAfter)
        {
            LogSaveGivenUp(exception, _pending.Count);
            _pending.Clear();
            _failingSince = null;
        }
    }

    /// <summary>
    /// Schreibt in einer Transaktion, je Nachricht ein INSERT: So steigen die Ids in Annahme-Reihenfolge (AK-11), das
    /// garantiert SaveChanges nicht. Nachrichten von Accounts, die es nicht mehr gibt, werden übersprungen (AK-17).
    /// </summary>
    internal async Task SaveAsync(IReadOnlyCollection<ChatHistoryEntry> messages, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var message in messages)
        {
            var sentAt = message.SentAt.ToUnixTimeMilliseconds();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "ChatMessages" ("AccountId", "Text", "SentAt")
                SELECT {message.AccountId}, {message.Text}, {sentAt}
                WHERE EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Id" = {message.AccountId})
                """, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Die jüngsten Nachrichten der letzten 24 Stunden, älteste zuerst (AK-15). Genau 24 Stunden alt zählt nicht mehr.</summary>
    internal async Task<IReadOnlyList<ChatHistoryEntry>> LoadRecentAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var cutoff = now - MaxAge;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var newestFirst = await (
            from m in db.ChatMessages
            join a in db.Users on m.AccountId equals a.Id
            where m.SentAt > cutoff
            orderby m.SentAt descending, m.Id descending
            select new { m.AccountId, a.UserName, m.Text, m.SentAt })
            .Take(GameSettings.ChatHistoryCount)
            .ToListAsync(cancellationToken);

        return [.. newestFirst.AsEnumerable().Reverse().Select(m => new ChatHistoryEntry(m.AccountId, m.UserName ?? "", m.Text, m.SentAt))];
    }

    /// <summary>Löscht höchstens <paramref name="batchSize"/> Nachrichten, die älter als 24 Stunden sind (AK-16); genau 24 Stunden alt bleibt bis zum nächsten Mal.</summary>
    internal async Task<int> DeleteExpiredAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = now - MaxAge;
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return await db.ChatMessages.Where(m => m.SentAt < cutoff).OrderBy(m => m.SentAt).Take(batchSize).ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Ein voller Stapel heißt, es liegt noch mehr: Dann folgt der nächste im nächsten Durchlauf. So blockiert auch eine große
    /// Löschung andere Schreiber nie lange (SQLite: ein Schreiber je Datei, 30 s Wartezeit).
    /// </summary>
    private async Task CleanupIfDueAsync(CancellationToken stoppingToken)
    {
        var now = time.GetUtcNow();
        if (now < _nextCleanup)
        {
            return;
        }

        try
        {
            if (await DeleteExpiredAsync(now, cleanupBatchSize, stoppingToken) < cleanupBatchSize)
            {
                _nextCleanup = now + CleanupInterval;
            }
        }
        catch (Exception ex)
        {
            // Ausgeliefert wird trotzdem nichts Altes (AK-16); der nächste Versuch kommt in einer Stunde.
            if (!stoppingToken.IsCancellationRequested)
            {
                LogCleanupFailed(ex);
            }

            _nextCleanup = now + CleanupInterval;
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Chat-Verlauf nicht geladen, Start ohne alten Verlauf")]
    private partial void LogLoadFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Speichern von {Count} Chat-Nachrichten fehlgeschlagen, wird wiederholt")]
    private partial void LogSaveFailed(Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} Chat-Nachrichten nicht gespeichert, Versuche aufgegeben")]
    private partial void LogSaveGivenUp(Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Count} Chat-Nachrichten verworfen, die Warteschlange war voll")]
    private partial void LogDropped(long count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} Chat-Nachrichten beim Herunterfahren nicht gespeichert")]
    private partial void LogShutdownLost(int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Löschen alter Chat-Nachrichten fehlgeschlagen")]
    private partial void LogCleanupFailed(Exception exception);
}
