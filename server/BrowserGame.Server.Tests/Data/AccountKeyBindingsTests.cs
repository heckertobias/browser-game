using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace BrowserGame.Server.Tests;

/// <summary>002: <see cref="Account.KeyBindings"/> – Protobuf-Bytes der Tastenbelegung, <c>NULL</c> = Standard des Clients.</summary>
public sealed class AccountKeyBindingsTests : IAsyncLifetime
{
    private readonly string _connectionString = TestDatabase.NewConnectionString("bg-keys");

    private ServiceProvider _services = null!;
    private DatabaseProvider _provider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

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

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        TestDatabase.Drop(_connectionString);
    }

    private async Task<byte[]?> KeyBindingsAsync(string id)
    {
        await using var scope = _services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<GameDbContext>().Users;
        return (await users.SingleAsync(u => u.Id == id, Ct)).KeyBindings;
    }

    // AK-9, AK-10
    [Fact]
    public async Task KeyBindings_is_optional_with_max_length_512()
    {
        await using var scope = _services.CreateAsyncScope();
        var property = scope.ServiceProvider.GetRequiredService<GameDbContext>().Model
            .FindEntityType(typeof(Account))!.FindProperty(nameof(Account.KeyBindings))!;
        Assert.True(property.IsNullable);
        Assert.Equal(512, property.GetMaxLength());
    }

    // AK-10
    [Fact]
    public async Task Existing_accounts_have_no_key_bindings_after_migration()
    {
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            await db.GetService<IMigrator>().MigrateAsync("AddChatMessages", Ct);
            await db.Database.ExecuteSqlRawAsync(
                """INSERT INTO "AspNetUsers" ("Id","UserName","EmailConfirmed","PhoneNumberConfirmed","TwoFactorEnabled","LockoutEnabled","AccessFailedCount") VALUES ('old','Alt',FALSE,FALSE,FALSE,FALSE,0)""",
                Ct);
        }

        await _services.MigrateAsync<GameDbContext>(_provider);

        await using (var scope = _services.CreateAsyncScope())
        {
            var applied = await scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.GetAppliedMigrationsAsync(Ct);
            Assert.Contains(applied, m => m.EndsWith("_AddKeyBindings", StringComparison.Ordinal));
        }

        Assert.Null(await KeyBindingsAsync("old"));
    }

    // AK-9, AK-10
    [Fact]
    public async Task KeyBindings_are_stored_unchanged_and_can_be_reset_to_null()
    {
        await _services.MigrateAsync<GameDbContext>(_provider);
        var bytes = Enumerable.Range(0, 512).Select(i => (byte)i).ToArray();

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.Users.Add(new Account { Id = "k", UserName = "Keys", NormalizedUserName = "KEYS", KeyBindings = bytes });
            db.Users.Add(new Account { Id = "d", UserName = "Default", NormalizedUserName = "DEFAULT" });
            await db.SaveChangesAsync(Ct);
        }

        Assert.Equal(bytes, await KeyBindingsAsync("k"));
        Assert.Null(await KeyBindingsAsync("d"));

        await using (var scope = _services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<GameDbContext>().Users;
            Assert.Equal(1, await users.Where(u => u.Id == "k").ExecuteUpdateAsync(s => s.SetProperty(a => a.KeyBindings, (byte[]?)null), Ct));
        }

        Assert.Null(await KeyBindingsAsync("k"));
    }
}
