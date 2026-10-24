using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BrowserGame.Data;

internal enum DatabaseProvider
{
    Sqlite,
    Postgres,
}

/// <summary>Wählt die Datenbank per Konfiguration: <c>Database:Provider</c> und <c>ConnectionStrings:Game</c> (ADR 0004).</summary>
internal static class DatabaseSetup
{
    public static DatabaseProvider AddGameDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var provider = configuration.GetValue("Database:Provider", DatabaseProvider.Sqlite);
        var connectionString = configuration.GetConnectionString("Game")
            ?? throw new InvalidOperationException("ConnectionStrings:Game fehlt.");

        services.Configure<IdentityOptions>(ConfigureStores);
        _ = provider switch
        {
            DatabaseProvider.Sqlite => services.AddDbContext<GameDbContext, SqliteGameDbContext>(o => o.UseSqlite(connectionString)),
            DatabaseProvider.Postgres => services.AddDbContext<GameDbContext, PostgresGameDbContext>(o => o.UseNpgsql(connectionString)),
            _ => throw new InvalidOperationException($"Unbekannter Database:Provider '{provider}'."),
        };
        return provider;
    }

    /// <summary>Gemeinsam für Laufzeit und <c>dotnet ef</c>, damit beide dasselbe Identity-Schema sehen.</summary>
    internal static void ConfigureStores(IdentityOptions options) => options.Stores.SchemaVersion = GameDbContext.IdentitySchemaVersion;

    /// <summary>Minimaler Service-Provider mit den Identity-Optionen für die Design-Time-Factories.</summary>
    internal static IServiceProvider DesignTimeServices() =>
        new ServiceCollection().Configure<IdentityOptions>(ConfigureStores).BuildServiceProvider();

    /// <summary>Migrationen beim Start anwenden (nur eine Instanz); SQLite im WAL-Modus betreiben.</summary>
    public static async Task MigrateGameDatabaseAsync(this IServiceProvider services, DatabaseProvider provider)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        if (provider == DatabaseProvider.Sqlite)
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        await db.Database.MigrateAsync();
    }
}

/// <summary>Nur für <c>dotnet ef</c> (Migrationen erzeugen); zur Laufzeit nicht benutzt.</summary>
internal sealed class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<SqliteGameDbContext>
{
    public SqliteGameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteGameDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .UseApplicationServiceProvider(DatabaseSetup.DesignTimeServices())
            .Options);
}

/// <summary>Nur für <c>dotnet ef</c> (Migrationen erzeugen); zur Laufzeit nicht benutzt.</summary>
internal sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresGameDbContext>
{
    public PostgresGameDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresGameDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time")
            .UseApplicationServiceProvider(DatabaseSetup.DesignTimeServices())
            .Options);
}
