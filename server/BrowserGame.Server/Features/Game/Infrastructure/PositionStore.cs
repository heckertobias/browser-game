using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Features.Game;

/// <summary>Position einer Figur zum Speichern; im Tick-Thread kopiert, damit spätere Bewegung sie nicht ändert.</summary>
internal readonly record struct PositionToSave(string AccountId, double X, double Y);

/// <summary>
/// Schreibt die Positionen aus dem <see cref="GameLoop"/> gebündelt in die Datenbank (003 AK-18, AK-19).
/// Liest, bis der GameLoop den Channel beim Herunterfahren schließt; darum vor dem GameLoop registrieren,
/// damit er nach ihm gestoppt wird.
/// </summary>
internal sealed partial class PositionStore(
    GameLoop game, IServiceScopeFactory scopes, TimeProvider time, ILogger<PositionStore> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Nicht auf stoppingToken abbrechen: Der letzte Stand kommt erst beim Herunterfahren (AK-19).
        var batches = game.PositionBatches;
        while (await batches.WaitToReadAsync(CancellationToken.None))
        {
            var latest = new Dictionary<string, PositionToSave>();
            while (batches.TryRead(out var batch))
            {
                foreach (var position in batch)
                {
                    latest[position.AccountId] = position;
                }
            }

            await SaveWithRetryAsync([.. latest.Values]);
        }
    }

    /// <summary>
    /// Upsert aller Positionen in einer Transaktion. Accounts, die es nicht mehr gibt, werden übersprungen:
    /// Ein Batch kann vor dem Löschen entstanden und erst danach geschrieben werden (003 AK-16).
    /// </summary>
    internal async Task SaveAsync(IReadOnlyCollection<PositionToSave> positions, CancellationToken cancellationToken)
    {
        if (positions.Count == 0)
        {
            return;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var now = time.GetUtcNow();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        foreach (var p in positions)
        {
            // Gleiche Syntax für SQLite und PostgreSQL. SQLite braucht bei INSERT … SELECT … ON CONFLICT ein WHERE.
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO "PlayerPositions" ("AccountId", "X", "Y", "UpdatedAt")
                SELECT {p.AccountId}, {p.X}, {p.Y}, {now}
                WHERE EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "Id" = {p.AccountId})
                ON CONFLICT ("AccountId") DO UPDATE SET "X" = excluded."X", "Y" = excluded."Y", "UpdatedAt" = excluded."UpdatedAt"
                """, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task SaveWithRetryAsync(PositionToSave[] positions)
    {
        try
        {
            await SaveAsync(positions, CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Z. B. PostgreSQL: Account wurde zwischen EXISTS und FK-Prüfung gelöscht; im zweiten Versuch fehlt er.
            LogSaveRetried(ex, positions.Length);
            try
            {
                await SaveAsync(positions, CancellationToken.None);
            }
            catch (Exception retryEx) when (retryEx is not OperationCanceledException)
            {
                LogSaveFailed(retryEx, positions.Length);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Speichern von {Count} Positionen fehlgeschlagen, neuer Versuch")]
    private partial void LogSaveRetried(Exception exception, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "{Count} Positionen nicht gespeichert")]
    private partial void LogSaveFailed(Exception exception, int count);
}
