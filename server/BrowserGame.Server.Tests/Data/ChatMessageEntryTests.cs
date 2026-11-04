using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;

namespace BrowserGame.Server.Tests;

/// <summary>011: Tabelle ChatMessages – Modell, Cascade-Delete und die Abfragen, die der ChatStore (T6) braucht.</summary>
public sealed class ChatMessageEntryTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Cutoff = Now.AddHours(-24);

    private readonly string _connectionString = TestDatabase.NewConnectionString("bg-chat");
    private ServiceProvider _services = null!;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:Provider"] = GameServerFactory.Provider,
            ["ConnectionStrings:Game"] = _connectionString,
        }).Build();

        var services = new ServiceCollection().AddLogging();
        var provider = services.AddGameDatabase(configuration);
        _services = services.BuildServiceProvider();
        await _services.MigrateAsync<GameDbContext>(provider);

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        db.Users.Add(new Account { Id = "a", UserName = "Anna", NormalizedUserName = "ANNA" });
        db.Users.Add(new Account { Id = "b", UserName = "Ben", NormalizedUserName = "BEN" });
        await db.SaveChangesAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        TestDatabase.Drop(_connectionString);
    }

    private static ChatMessageEntry Message(string accountId, string text, DateTimeOffset sentAt) =>
        new() { AccountId = accountId, Text = text, SentAt = sentAt };

    private sealed record Row(string? Name, string Text, DateTimeOffset SentAt);

    private async Task SaveAsync(params ChatMessageEntry[] messages)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        db.ChatMessages.AddRange(messages);
        await db.SaveChangesAsync(Ct);
    }

    private async Task<List<string>> TextsAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var texts = await db.ChatMessages.Select(m => m.Text).ToListAsync(Ct);
        return [.. texts.Order(StringComparer.Ordinal)];
    }

    private async Task<List<Row>> RecentAsync()
    {
        await using var scope = _services.CreateAsyncScope();
        return await RecentAsync(scope.ServiceProvider.GetRequiredService<GameDbContext>());
    }

    // Form der Abfrage aus ChatStore.LoadRecentAsync (T6)
    private static Task<List<Row>> RecentAsync(GameDbContext db) =>
        (from m in db.ChatMessages
         join a in db.Users on m.AccountId equals a.Id
         where m.SentAt > Cutoff
         orderby m.SentAt descending, m.Id descending
         select new Row(a.UserName, m.Text, m.SentAt))
        .Take(50).ToListAsync(Ct);

    private async Task SaveAgeSetAsync()
    {
        await SaveAsync(
            Message("a", "neu", Now.AddHours(-23)),
            Message("a", "grenze", Cutoff),
            Message("a", "alt", Now.AddHours(-25)));
    }

    // AK-15, AK-17
    [Fact]
    public async Task Model_has_required_text_index_on_SentAt_and_cascading_account_key()
    {
        await using var scope = _services.CreateAsyncScope();
        var entity = scope.ServiceProvider.GetRequiredService<GameDbContext>().Model.FindEntityType(typeof(ChatMessageEntry))!;

        Assert.Equal("ChatMessages", entity.GetTableName());
        var text = entity.FindProperty(nameof(ChatMessageEntry.Text))!;
        Assert.False(text.IsNullable);
        Assert.Equal(200, text.GetMaxLength());
        Assert.Equal(ValueGenerated.OnAdd, entity.FindProperty(nameof(ChatMessageEntry.Id))!.ValueGenerated);
        Assert.Contains(entity.GetIndexes(), i => i.Properties is [{ Name: nameof(ChatMessageEntry.SentAt) }]);
        var key = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(Account), key.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, key.DeleteBehavior);
    }

    // AK-15
    [Fact]
    public async Task Migration_AddChatMessages_is_applied()
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var applied = await db.Database.GetAppliedMigrationsAsync(Ct);
        Assert.Contains(applied, m => m.EndsWith("_AddChatMessages", StringComparison.Ordinal));
    }

    // AK-17
    [Fact]
    public async Task Deleting_an_account_deletes_only_its_messages()
    {
        await SaveAsync(Message("a", "von-a", Now), Message("b", "von-b", Now));

        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            await db.Users.Where(u => u.Id == "a").ExecuteDeleteAsync(Ct);
        }

        Assert.Equal(["von-b"], await TextsAsync());
    }

    // AK-17
    [Fact]
    public async Task Message_needs_an_existing_account()
    {
        await Assert.ThrowsAsync<DbUpdateException>(() => SaveAsync(Message("fehlt", "x", Now)));
    }

    // AK-15
    [Fact]
    public async Task Recent_returns_the_newest_50_newest_first_with_sender_name()
    {
        await SaveAsync([.. Enumerable.Range(0, 60).Select(i => Message("a", $"m{i}", Now.AddMinutes(-i)))]);

        var recent = await RecentAsync();

        Assert.Equal(Enumerable.Range(0, 50).Select(i => $"m{i}"), recent.Select(r => r.Text));
        Assert.All(recent, r => Assert.Equal("Anna", r.Name));
    }

    // AK-15, AK-16
    [Fact]
    public async Task Recent_skips_messages_older_than_24_hours()
    {
        await SaveAgeSetAsync();

        var recent = await RecentAsync();

        Assert.Equal(["neu"], recent.Select(r => r.Text));
    }

    // AK-16
    [Fact]
    public async Task Cleanup_deletes_only_messages_older_than_24_hours()
    {
        await SaveAgeSetAsync();

        int deleted;
        await using (var scope = _services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            deleted = await db.ChatMessages.Where(m => m.SentAt < Cutoff).ExecuteDeleteAsync(Ct);
        }

        Assert.Equal(1, deleted);
        Assert.Equal(["grenze", "neu"], await TextsAsync());
    }

    // AK-15
    [Fact]
    public async Task Messages_with_the_same_time_keep_their_order_through_Id()
    {
        await SaveAsync(Message("a", "erste", Now));
        await SaveAsync(Message("a", "zweite", Now));

        var recent = await RecentAsync();

        Assert.Equal(["zweite", "erste"], recent.Select(r => r.Text));
    }

    // AK-15
    [Fact]
    public async Task SentAt_keeps_the_instant_and_sorts_across_offsets()
    {
        var frueher = new DateTimeOffset(2026, 11, 2, 12, 0, 0, 123, TimeSpan.FromHours(2));
        var spaeter = new DateTimeOffset(2026, 11, 2, 11, 0, 0, TimeSpan.Zero);
        await SaveAsync(Message("a", "spaeter", spaeter), Message("a", "frueher", frueher));

        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var ordered = await db.ChatMessages.OrderBy(m => m.SentAt).ToListAsync(Ct);

        Assert.Equal(["frueher", "spaeter"], ordered.Select(m => m.Text));
        Assert.Equal(frueher, ordered[0].SentAt);
        Assert.Equal(TimeSpan.Zero, ordered[0].SentAt.Offset);
    }

    // AK-15
    [Fact]
    public async Task Text_of_200_characters_with_markup_is_stored_unchanged()
    {
        var text = "<b>" + new string('x', 197);
        Assert.Equal(200, text.Length);
        await SaveAsync(Message("a", text, Now));

        Assert.Equal([text], await TextsAsync());
    }

    // AK-15
    [Fact]
    public async Task Text_with_emoji_is_stored_unchanged()
    {
        const string text = "<b>🙂 hallo";
        await SaveAsync(Message("a", text, Now));

        Assert.Equal([text], await TextsAsync());
    }
}
