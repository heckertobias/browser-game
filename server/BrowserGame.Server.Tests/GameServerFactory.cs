using BrowserGame.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Startet den echten Server im Speicher, mit eigener Datenbank pro Factory und Platzhalter-wwwroot.
/// Datenbank: SQLite-Temp-Datei (Standard) oder PostgreSQL, wenn <c>TEST_DB_PROVIDER=Postgres</c> und
/// <c>TEST_POSTGRES</c> (Verbindung ohne Datenbanknamen) gesetzt sind – so laufen dieselben Tests gegen beide (ADR 0004).
/// </summary>
public sealed class GameServerFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Directory.CreateTempSubdirectory("browsergame-test").FullName;

    public GameServerFactory()
    {
        var play = Path.Combine(WebRoot, "play");
        Directory.CreateDirectory(Path.Combine(play, "assets"));
        // Fallback, falls kein Client-Build vorliegt; liegt einer in wwwroot/play, wird ggf. der echte ausgeliefert.
        File.WriteAllText(Path.Combine(play, "index.html"), "<!doctype html><div id=\"root\"></div>");
        File.WriteAllText(Path.Combine(play, "assets", "test-asset.js"), "// test-asset");
    }

    public static string Provider => Environment.GetEnvironmentVariable("TEST_DB_PROVIDER") ?? nameof(DatabaseProvider.Sqlite);

    private string WebRoot => Path.Combine(_root, "wwwroot");

    /// <summary>Verbindungszeichenfolge dieser Factory; bleibt gleich, damit ein zweiter Server dieselbe DB nutzen kann.</summary>
    public string ConnectionString { get; } = Provider == nameof(DatabaseProvider.Postgres)
        ? $"{Environment.GetEnvironmentVariable("TEST_POSTGRES")};Database=bg_test_{Guid.NewGuid():N}"
        : $"Data Source={Path.Combine(Path.GetTempPath(), $"bg-test-{Guid.NewGuid():N}.db")}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseWebRoot(WebRoot);
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
        Directory.Delete(_root, recursive: true);
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
