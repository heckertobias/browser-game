using BrowserGame.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Tests;

/// <summary>
/// Startet das Admin im Speicher mit eigener Datenbank (SQLite-Temp-Datei oder PostgreSQL über
/// <c>TEST_DB_PROVIDER=Postgres</c> + <c>TEST_POSTGRES</c>, wie die Spiel-Tests).
/// </summary>
public sealed class AdminFactory : WebApplicationFactory<Program>
{
    public static string Provider => Environment.GetEnvironmentVariable("TEST_DB_PROVIDER") ?? nameof(DatabaseProvider.Sqlite);

    public string ConnectionString { get; } = Provider == nameof(DatabaseProvider.Postgres)
        ? $"{Environment.GetEnvironmentVariable("TEST_POSTGRES")};Database=bg_admin_test_{Guid.NewGuid():N}"
        : $"Data Source={Path.Combine(Path.GetTempPath(), $"bg-admin-test-{Guid.NewGuid():N}.db")}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", Provider);
        builder.UseSetting("ConnectionStrings:Game", ConnectionString);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Provider == nameof(DatabaseProvider.Postgres))
        {
            using var scope = Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.EnsureDeleted();
        }

        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        if (Provider == nameof(DatabaseProvider.Sqlite))
        {
            var file = new SqliteConnectionStringBuilder(ConnectionString).DataSource;
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(file + suffix);
            }
        }
    }
}
