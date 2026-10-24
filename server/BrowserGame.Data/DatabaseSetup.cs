using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BrowserGame.Data;

internal enum DatabaseProvider
{
    Sqlite,
    Postgres,
}

/// <summary>
/// Wählt die Datenbank per Konfiguration: <c>Database:Provider</c> und <c>ConnectionStrings:Game</c> (ADR 0004).
/// Spiel und Admin teilen sich Datenbank und Data-Protection-Schlüssel (ADR 0006).
/// </summary>
internal static class DatabaseSetup
{
    /// <summary>Gemeinsamer Name des Schlüsselrings: Spiel und Admin müssen das SMTP-Passwort beide entschlüsseln können.</summary>
    public const string DataProtectionApplicationName = "browsergame";

    public static DatabaseProvider AddGameDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var (provider, connectionString) = Read(configuration);

        services.Configure<IdentityOptions>(ConfigureStores);
        _ = provider switch
        {
            DatabaseProvider.Sqlite => services.AddDbContext<GameDbContext, SqliteGameDbContext>(o => o.UseSqlite(connectionString)),
            DatabaseProvider.Postgres => services.AddDbContext<GameDbContext, PostgresGameDbContext>(o => o.UseNpgsql(connectionString)),
            _ => throw new InvalidOperationException($"Unbekannter Database:Provider '{provider}'."),
        };

        services.AddDataProtection().SetApplicationName(DataProtectionApplicationName).PersistKeysToDbContext<GameDbContext>();
        services.TryAddSingletonTimeProvider();
        services.AddScoped<SmtpSettingsStore>();
        return provider;
    }

    /// <summary>Zusätzlich die Admin-Tabellen (nur im Admin-Dienst).</summary>
    public static void AddAdminDatabase(this IServiceCollection services, IConfiguration configuration)
    {
        var (provider, connectionString) = Read(configuration);
        _ = provider switch
        {
            DatabaseProvider.Sqlite => services.AddDbContext<AdminDbContext, SqliteAdminDbContext>(o =>
                o.UseSqlite(connectionString, x => x.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))),
            DatabaseProvider.Postgres => services.AddDbContext<AdminDbContext, PostgresAdminDbContext>(o =>
                o.UseNpgsql(connectionString, x => x.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))),
            _ => throw new InvalidOperationException($"Unbekannter Database:Provider '{provider}'."),
        };
    }

    /// <summary>Gemeinsam für Laufzeit und <c>dotnet ef</c>, damit beide dasselbe Identity-Schema sehen.</summary>
    internal static void ConfigureStores(IdentityOptions options) => options.Stores.SchemaVersion = GameDbContext.IdentitySchemaVersion;

    /// <summary>Minimaler Service-Provider mit den Identity-Optionen für die Design-Time-Factories.</summary>
    internal static IServiceProvider DesignTimeServices() =>
        new ServiceCollection().Configure<IdentityOptions>(ConfigureStores).BuildServiceProvider();

    /// <summary>
    /// Migrationen beim Start anwenden; SQLite im WAL-Modus (zwei Prozesse auf derselben Datei, ADR 0006).
    /// EF Core sperrt die Migration, falls Spiel und Admin gleichzeitig starten.
    /// </summary>
    public static async Task MigrateAsync<TContext>(this IServiceProvider services, DatabaseProvider provider)
        where TContext : DbContext
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        if (provider == DatabaseProvider.Sqlite)
        {
            await db.Database.OpenConnectionAsync();
            await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");
        }

        await db.Database.MigrateAsync();
    }

    private static (DatabaseProvider Provider, string ConnectionString) Read(IConfiguration configuration) =>
        (configuration.GetValue("Database:Provider", DatabaseProvider.Sqlite),
         configuration.GetConnectionString("Game") ?? throw new InvalidOperationException("ConnectionStrings:Game fehlt."));

    private static void TryAddSingletonTimeProvider(this IServiceCollection services)
    {
        if (!services.Any(s => s.ServiceType == typeof(TimeProvider)))
        {
            services.AddSingleton(TimeProvider.System);
        }
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

/// <summary>Nur für <c>dotnet ef</c> (Migrationen erzeugen); zur Laufzeit nicht benutzt.</summary>
internal sealed class SqliteAdminDesignTimeFactory : IDesignTimeDbContextFactory<SqliteAdminDbContext>
{
    public SqliteAdminDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteAdminDbContext>()
            .UseSqlite("Data Source=design-time.db", x => x.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))
            .UseApplicationServiceProvider(DatabaseSetup.DesignTimeServices())
            .Options);
}

/// <summary>Nur für <c>dotnet ef</c> (Migrationen erzeugen); zur Laufzeit nicht benutzt.</summary>
internal sealed class PostgresAdminDesignTimeFactory : IDesignTimeDbContextFactory<PostgresAdminDbContext>
{
    public PostgresAdminDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PostgresAdminDbContext>()
            .UseNpgsql("Host=localhost;Database=design_time", x => x.MigrationsHistoryTable(AdminDbContext.MigrationsHistoryTable))
            .UseApplicationServiceProvider(DatabaseSetup.DesignTimeServices())
            .Options);
}
