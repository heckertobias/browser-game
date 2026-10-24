using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

public sealed class DatabaseTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    [Fact] // Laufzeit-Modell und Migrationen müssen übereinstimmen – inklusive Passkey-Tabelle (Identity-Schema v3)
    public void Runtime_model_matches_migrations_and_contains_passkeys()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();

        Assert.False(db.Database.HasPendingModelChanges());
        Assert.NotNull(db.Model.FindEntityType(typeof(IdentityUserPasskey<string>)));
        Assert.Empty(db.Database.GetPendingMigrations());
    }

    [Fact] // AK-19 Grundlage: Daten überleben einen Neustart (neue Server-Instanz, dieselbe Datenbank)
    public async Task Data_survives_server_restart()
    {
        var ct = TestContext.Current.CancellationToken;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Users.Add(new Account { Id = "restart", UserName = "Restart", NormalizedUserName = "RESTART" });
            await db.SaveChangesAsync(ct);
        }

        await using var second = factory.WithWebHostBuilder(_ => { });
        using var secondScope = second.Services.CreateScope();
        var reloaded = await secondScope.ServiceProvider.GetRequiredService<GameDbContext>().Users.FindAsync(["restart"], ct);
        Assert.Equal("Restart", reloaded?.UserName);
    }

    [Fact]
    public async Task Sqlite_backup_writes_a_consistent_copy()
    {
        Assert.SkipUnless(GameServerFactory.Provider == nameof(DatabaseProvider.Sqlite), "nur SQLite");
        var ct = TestContext.Current.CancellationToken;
        var target = Path.Combine(Path.GetTempPath(), $"bg-backup-{Guid.NewGuid():N}", "game.db");
        try
        {
            var backup = ActivatorUtilities.CreateInstance<SqliteBackupService>(factory.Services);
            await backup.BackupAsync(target, ct);
            await backup.BackupAsync(target, ct); // überschreibt eine vorhandene Kopie

            await using var connection = new SqliteConnection($"Data Source={target};Mode=ReadOnly");
            await connection.OpenAsync(ct);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE name = 'AspNetUsers'";
            Assert.Equal(1L, await command.ExecuteScalarAsync(ct));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(Path.GetDirectoryName(target)!, recursive: true);
        }
    }

    [Theory]
    [InlineData("2026-10-24T03:00:00+02:00", 50)]   // heute 03:50
    [InlineData("2026-10-24T03:50:00+02:00", 1440)] // gerade gelaufen → morgen
    [InlineData("2026-10-24T12:00:00+02:00", 950)]  // morgen 03:50
    public void Backup_is_scheduled_for_next_0350(string now, int expectedMinutes)
    {
        var delay = SqliteBackupService.DelayUntilNext(DateTimeOffset.Parse(now, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(3, 50));

        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), delay);
    }
}
