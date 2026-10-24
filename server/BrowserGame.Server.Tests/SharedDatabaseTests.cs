using BrowserGame.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace BrowserGame.Server.Tests;

/// <summary>Spiel- und Admin-Kontext teilen sich eine Datenbank (ADR 0006) – beide Migrationen müssen nebeneinander passen.</summary>
public sealed class SharedDatabaseTests : IAsyncLifetime
{
    private readonly string _connectionString = GameServerFactory.Provider == nameof(DatabaseProvider.Postgres)
        ? $"{Environment.GetEnvironmentVariable("TEST_POSTGRES")};Database=bg_shared_{Guid.NewGuid():N}"
        : $"Data Source={Path.Combine(Path.GetTempPath(), $"bg-shared-{Guid.NewGuid():N}.db")}";

    private ServiceProvider _services = null!;
    private DatabaseProvider _provider;

    public ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = GameServerFactory.Provider,
            ["ConnectionStrings:Game"] = _connectionString,
        }).Build();

        var services = new ServiceCollection().AddLogging();
        _provider = services.AddGameDatabase(configuration);
        services.AddAdminDatabase(configuration);
        _services = services.BuildServiceProvider();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Game_and_admin_migrations_apply_to_the_same_database_in_any_order()
    {
        await _services.MigrateAsync<AdminDbContext>(_provider);
        await _services.MigrateAsync<GameDbContext>(_provider);

        await using var scope = _services.CreateAsyncScope();
        var game = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var admin = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        Assert.Empty(await game.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await admin.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.False(admin.Database.HasPendingModelChanges());
    }

    [Fact] // 004 AK-2: Spieler und Admins sind getrennt – gleicher Name in beiden Tabellen ist kein Konflikt
    public async Task Player_and_admin_accounts_live_in_separate_tables()
    {
        await _services.MigrateAsync<GameDbContext>(_provider);
        await _services.MigrateAsync<AdminDbContext>(_provider);
        var ct = TestContext.Current.CancellationToken;

        await using var scope = _services.CreateAsyncScope();
        var game = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var admin = scope.ServiceProvider.GetRequiredService<AdminDbContext>();
        game.Users.Add(new Account { Id = "p", UserName = "Tobias", NormalizedUserName = "TOBIAS", Email = "t@example.org", NormalizedEmail = "T@EXAMPLE.ORG" });
        admin.Users.Add(new AdminUser { Id = "a", UserName = "Tobias", NormalizedUserName = "TOBIAS", Email = "t@example.org", NormalizedEmail = "T@EXAMPLE.ORG" });
        await game.SaveChangesAsync(ct);
        await admin.SaveChangesAsync(ct);

        Assert.Equal(1, await game.Users.CountAsync(ct));
        Assert.Equal(1, await admin.Users.CountAsync(ct));
    }

    public async ValueTask DisposeAsync()
    {
        if (_provider == DatabaseProvider.Postgres)
        {
            await using var scope = _services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.EnsureDeletedAsync();
        }

        await _services.DisposeAsync();
        SqliteConnection.ClearAllPools();
        if (_provider == DatabaseProvider.Sqlite)
        {
            var file = new SqliteConnectionStringBuilder(_connectionString).DataSource;
            foreach (var suffix in new[] { "", "-wal", "-shm" })
            {
                File.Delete(file + suffix);
            }
        }
    }
}
