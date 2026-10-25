using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace BrowserGame.Server.Tests;

/// <summary>006: <see cref="Account.Language"/> – bestehende Accounts werden deutsch, neue speichern ihre Sprache.</summary>
public sealed class AccountLanguageMigrationTests : IAsyncLifetime
{
    private readonly string _connectionString = TestDatabase.NewConnectionString("bg-lang");

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
        _services = services.BuildServiceProvider();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Existing_accounts_get_german_after_migration()
    {
        var ct = TestContext.Current.CancellationToken;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("Initial", ct);
            await db.Database.ExecuteSqlRawAsync(
                """INSERT INTO "AspNetUsers" ("Id","UserName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount") VALUES ('old','Alt',FALSE,FALSE,FALSE,FALSE,0)""",
                ct);
        }

        await _services.MigrateAsync<GameDbContext>(_provider);

        await using var check = _services.CreateAsyncScope();
        var users = check.ServiceProvider.GetRequiredService<GameDbContext>().Users;
        Assert.Equal("de", (await users.SingleAsync(u => u.Id == "old", ct)).Language);
    }

    [Fact]
    public async Task Language_is_required_with_max_length_8()
    {
        await using var scope = _services.CreateAsyncScope();
        var property = scope.ServiceProvider.GetRequiredService<GameDbContext>().Model
            .FindEntityType(typeof(Account))!.FindProperty(nameof(Account.Language))!;
        Assert.False(property.IsNullable);
        Assert.Equal(8, property.GetMaxLength());
    }

    [Fact]
    public async Task New_account_is_stored_with_its_language()
    {
        await _services.MigrateAsync<GameDbContext>(_provider);
        var ct = TestContext.Current.CancellationToken;

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Users.Add(new Account { Id = "e", UserName = "Eng", NormalizedUserName = "ENG", Language = "en" });
            db.Users.Add(new Account { Id = "d", UserName = "Deu", NormalizedUserName = "DEU" });
            await db.SaveChangesAsync(ct);
        }

        await using var check = _services.CreateAsyncScope();
        var users = check.ServiceProvider.GetRequiredService<GameDbContext>().Users;
        Assert.Equal("en", (await users.SingleAsync(u => u.Id == "e", ct)).Language);
        Assert.Equal("de", (await users.SingleAsync(u => u.Id == "d", ct)).Language);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        TestDatabase.Drop(_connectionString);
    }
}
