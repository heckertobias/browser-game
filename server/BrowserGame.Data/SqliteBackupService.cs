using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Data;

/// <summary>
/// Schreibt täglich eine konsistente Kopie der SQLite-Datenbank (<c>VACUUM INTO</c>), damit ein dateibasiertes
/// Backup (Homelab: restic um 04:00) nie eine halb geschriebene Datei erwischt (ADR 0004).
/// Aktiv nur bei SQLite und gesetztem <c>Database:BackupPath</c>.
/// </summary>
internal sealed partial class SqliteBackupService(
    IServiceScopeFactory scopes, IConfiguration configuration, TimeProvider time, ILogger<SqliteBackupService> logger) : BackgroundService
{
    /// <summary>Lokale Uhrzeit des Backups, vor dem Homelab-Backup um 04:00.</summary>
    private static readonly TimeOnly BackupTime = new(3, 50);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var target = configuration["Database:BackupPath"];
        if (string.IsNullOrWhiteSpace(target))
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNext(time.GetLocalNow(), BackupTime), time, stoppingToken);
            try
            {
                await BackupAsync(target, stoppingToken);
                LogBackupWritten(target);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogBackupFailed(ex, target);
            }
        }
    }

    internal static TimeSpan DelayUntilNext(DateTimeOffset now, TimeOnly at)
    {
        var next = new DateTimeOffset(DateOnly.FromDateTime(now.DateTime).ToDateTime(at), now.Offset);
        return next > now ? next - now : next.AddDays(1) - now;
    }

    internal async Task BackupAsync(string target, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(target))!);
        var temporary = target + ".tmp";
        File.Delete(temporary);

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        // VACUUM INTO akzeptiert keine Parameter; der Pfad stammt aus der Konfiguration, nicht von Spielern.
        var escaped = temporary.Replace("'", "''", StringComparison.Ordinal);
#pragma warning disable EF1002 // Pfad ist Konfiguration, kein Benutzereingabe
        await db.Database.ExecuteSqlRawAsync($"VACUUM INTO '{escaped}';", cancellationToken);
#pragma warning restore EF1002
        File.Move(temporary, target, overwrite: true);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "SQLite-Backup geschrieben: {Target}")]
    private partial void LogBackupWritten(string target);

    [LoggerMessage(Level = LogLevel.Error, Message = "SQLite-Backup nach {Target} fehlgeschlagen")]
    private partial void LogBackupFailed(Exception exception, string target);
}
