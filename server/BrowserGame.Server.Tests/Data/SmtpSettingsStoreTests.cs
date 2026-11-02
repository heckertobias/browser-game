using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

public sealed class SmtpSettingsStoreTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly SmtpSettings Settings = new("mx.example.net", 465, SmtpSecurity.SslOnConnect, "game@example.net", "geheim-123", "noreply@example.net", "Browser Game");

    private async Task<T> WithStoreAsync<T>(Func<SmtpSettingsStore, GameDbContext, Task<T>> action)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await action(scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>(), scope.ServiceProvider.GetRequiredService<GameDbContext>());
    }

    [Fact] // AK-7
    public async Task Saved_settings_can_be_read_back()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var loaded = await WithStoreAsync((store, _) => store.GetAsync());

        Assert.Equal(Settings, loaded);
    }

    [Fact] // AK-8: Passwort nur verschlüsselt in der DB
    public async Task Password_is_not_stored_in_plain_text()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var raw = await WithStoreAsync((_, db) => db.Settings.AsNoTracking().SingleAsync(s => s.Key == SmtpSettingsStore.Key));

        Assert.DoesNotContain("geheim-123", raw.Value, StringComparison.Ordinal);
        Assert.Contains("mx.example.net", raw.Value, StringComparison.Ordinal);
    }

    [Fact] // AK-8: leeres Passwortfeld = unverändert
    public async Task Empty_password_keeps_the_stored_one()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var changed = await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Password = "", Port = 587 }));
        var loaded = await WithStoreAsync((store, _) => store.GetAsync());

        Assert.Equal("geheim-123", loaded?.Password);
        Assert.Equal(587, loaded?.Port);
        Assert.Equal(["Port"], changed);
    }

    [Fact] // AK-12: Protokoll bekommt Feldnamen, keine Werte
    public async Task Changed_fields_name_password_without_value()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var changed = await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Password = "neu-456" }));

        Assert.Equal(["Password"], changed);
    }

    [Fact] // Spiel und Admin entschlüsseln mit demselben Schlüsselring (ADR 0006)
    public async Task Second_instance_with_same_database_can_decrypt_password()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        await using var other = factory.WithWebHostBuilder(_ => { });
        await using var scope = other.Services.CreateAsyncScope();
        var loaded = await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal("geheim-123", loaded?.Password);
    }

    [Fact] // 005 AK-18
    public async Task Empty_username_removes_the_stored_password()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var changed = await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Username = null, Password = "" }));
        var loaded = await WithStoreAsync((store, _) => store.GetAsync());

        Assert.Null(loaded!.Password);
        Assert.False(loaded.HasPassword);
        Assert.Null(loaded.Username);
        Assert.Equal(["Username", "Password"], changed);
    }

    [Theory] // 005 AK-18
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task Password_without_username_is_not_stored(string? username)
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings));

        var changed = await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Username = username, Password = "neu-456" }));
        var loaded = await WithStoreAsync((store, _) => store.GetAsync());
        var raw = await WithStoreAsync((_, db) => db.Settings.AsNoTracking().SingleAsync(s => s.Key == SmtpSettingsStore.Key));

        Assert.Null(loaded!.Password);
        Assert.Contains("Password", changed);
        Assert.Contains("\"ProtectedPassword\":null", raw.Value, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-18
    public async Task Username_brings_the_password_rule_back()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Username = null }));

        await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Password = "neu-456" }));
        var loaded = await WithStoreAsync((store, _) => store.GetAsync());

        Assert.Equal("neu-456", loaded!.Password);
    }

    [Fact] // 005 AK-18
    public async Task Password_typed_without_username_is_not_a_change()
    {
        await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Username = null }));

        var changed = await WithStoreAsync((store, _) => store.SaveAsync(Settings with { Username = null, Password = "neu-456" }));

        Assert.Empty(changed);
    }
}
