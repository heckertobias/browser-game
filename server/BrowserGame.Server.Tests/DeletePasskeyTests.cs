using System.Net;
using System.Text.Json;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>003 T9b: Account mit Passkey löschen (AK-16).</summary>
public sealed class DeletePasskeyTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private async Task<bool> ExistsAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email) is not null;
    }

    private static async Task<(HttpResponseMessage Response, string Html)> DeleteWithPasskeyAsync(FormClient client, SoftwareAuthenticator authenticator)
    {
        var (_, page) = await client.GetAsync("/Account/Delete");
        return await client.PostWithTokenAsync(
            "/Account/Delete?handler=Passkey",
            new Dictionary<string, string> { ["Credential"] = authenticator.Get(AccountTestFlows.PasskeyOptions(page)) },
            FormClient.AntiforgeryToken(page));
    }

    private async Task<(FormClient Client, AccountTestFlows.CreatedAccount Account)> PasskeyOnlyAsync(SoftwareAuthenticator authenticator)
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        await AccountTestFlows.AddPasskeyAsync(client, authenticator);
        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);
        return (client, account);
    }

    [Fact]
    public async Task Passkey_only_account_is_offered_passkey_confirmation()
    {
        using var authenticator = new SoftwareAuthenticator();
        var (client, _) = await PasskeyOnlyAsync(authenticator);
        using var _ = client;

        var (_, html) = await client.GetAsync("/Account/Delete");

        Assert.Contains("data-passkey=\"get\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Password\"", html, StringComparison.Ordinal);
        using var options = JsonDocument.Parse(AccountTestFlows.PasskeyOptions(html));
        var allowed = options.RootElement.GetProperty("allowCredentials").EnumerateArray().Select(e => e.GetProperty("id").GetString());
        Assert.Contains(authenticator.Id, allowed);
    }

    [Fact]
    public async Task Garbage_credential_keeps_account()
    {
        using var authenticator = new SoftwareAuthenticator();
        var (client, account) = await PasskeyOnlyAsync(authenticator);
        using var _ = client;
        var (_, page) = await client.GetAsync("/Account/Delete");

        var (response, html) = await client.PostWithTokenAsync(
            "/Account/Delete?handler=Passkey", new Dictionary<string, string> { ["Credential"] = "kein json" }, FormClient.AntiforgeryToken(page));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Der Passkey konnte nicht bestätigt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.True(await ExistsAsync(account.Email));
    }

    [Fact] // AK-16
    public async Task Passkey_confirmation_deletes_account_signs_out_and_frees_name()
    {
        using var authenticator = new SoftwareAuthenticator();
        var (client, account) = await PasskeyOnlyAsync(authenticator);
        using var _ = client;

        var (response, html) = await DeleteWithPasskeyAsync(client, authenticator);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dein Account wurde gelöscht.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bg.auth=;", StringComparison.Ordinal));
        Assert.False(await ExistsAsync(account.Email));
        using var other = factory.CreateFormClient();
        var (registered, _) = await AccountTestFlows.RegisterAsync(factory, other, AccountTestFlows.UniqueEmail(), account.Name);
        Assert.Equal(HttpStatusCode.Redirect, registered.StatusCode);
        Assert.Equal("/play", registered.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Passkey_of_another_account_is_rejected()
    {
        using var mine = new SoftwareAuthenticator();
        using var theirs = new SoftwareAuthenticator();
        var (client, account) = await PasskeyOnlyAsync(mine);
        using var _ = client;
        var (otherClient, other) = await AccountTestFlows.SignInAsync(factory);
        using var __ = otherClient;
        await AccountTestFlows.AddPasskeyAsync(otherClient, theirs);

        var (_, page) = await client.GetAsync("/Account/Delete");
        // Antwort des fremden Authenticators auf die Optionen dieser Seite (Challenge stimmt, Passkey gehört jemand anderem).
        var (_, html) = await client.PostWithTokenAsync(
            "/Account/Delete?handler=Passkey",
            new Dictionary<string, string> { ["Credential"] = theirs.Get(AccountTestFlows.PasskeyOptions(page)) },
            FormClient.AntiforgeryToken(page));

        Assert.Contains("Der Passkey konnte nicht bestätigt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.True(await ExistsAsync(account.Email));
        Assert.True(await ExistsAsync(other.Email));
    }

    [Fact] // AK-16
    public async Task Discoverable_assertion_of_another_account_is_rejected()
    {
        using var mine = new SoftwareAuthenticator();
        using var theirs = new SoftwareAuthenticator();
        var (client, account) = await PasskeyOnlyAsync(mine);
        using var _ = client;
        var (otherClient, other) = await AccountTestFlows.SignInAsync(factory);
        using var __ = otherClient;
        await AccountTestFlows.AddPasskeyAsync(otherClient, theirs);

        var (_, page) = await client.GetAsync("/Account/Delete");
        // Die Login-Seite ersetzt den Zeremonie-Zustand durch Optionen ohne allowCredentials: nur die Prüfung auf den eigenen Account greift.
        var (_, login) = await client.GetAsync("/Account/Login");
        var (response, html) = await client.PostWithTokenAsync(
            "/Account/Delete?handler=Passkey",
            new Dictionary<string, string> { ["Credential"] = theirs.Get(AccountTestFlows.PasskeyOptions(login)) },
            FormClient.AntiforgeryToken(page));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Der Passkey konnte nicht bestätigt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.True(await ExistsAsync(account.Email));
        Assert.True(await ExistsAsync(other.Email));
    }
}
