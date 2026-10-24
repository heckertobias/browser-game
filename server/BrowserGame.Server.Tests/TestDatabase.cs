using BrowserGame.Data;
using Microsoft.Data.Sqlite;
using Npgsql;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Eigene Test-Datenbank pro Factory: SQLite-Temp-Datei (Standard) oder PostgreSQL, wenn <c>TEST_DB_PROVIDER=Postgres</c>
/// und <c>TEST_POSTGRES</c> (Verbindung ohne Datenbanknamen) gesetzt sind – so laufen dieselben Tests gegen beide (ADR 0004).
/// Das Aufräumen kommt ohne die (dann schon entsorgten) App-Services aus.
/// </summary>
public static class TestDatabase
{
    public static string Provider => Environment.GetEnvironmentVariable("TEST_DB_PROVIDER") ?? nameof(DatabaseProvider.Sqlite);

    public static bool IsPostgres => Provider == nameof(DatabaseProvider.Postgres);

    public static string NewConnectionString(string prefix) => IsPostgres
        ? $"{Environment.GetEnvironmentVariable("TEST_POSTGRES")};Database={prefix}_{Guid.NewGuid():N}"
        : $"Data Source={Path.Combine(Path.GetTempPath(), $"{prefix}-{Guid.NewGuid():N}.db")}";

    public static void Drop(string connectionString)
    {
        if (IsPostgres)
        {
            NpgsqlConnection.ClearAllPools();
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            var database = builder.Database!;
            builder.Database = "postgres";
            using var connection = new NpgsqlConnection(builder.ConnectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{database.Replace("\"", "\"\"", StringComparison.Ordinal)}\" WITH (FORCE)";
            command.ExecuteNonQuery();
            return;
        }

        SqliteConnection.ClearAllPools();
        var file = new SqliteConnectionStringBuilder(connectionString).DataSource;
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            File.Delete(file + suffix);
        }
    }
}
