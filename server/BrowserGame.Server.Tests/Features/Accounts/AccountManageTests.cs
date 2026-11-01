using System.Net;
using System.Reflection;
using BrowserGame.Data;
using BrowserGame.Server.Features.Accounts.Presentation.Account;
using Microsoft.AspNetCore.RateLimiting;

namespace BrowserGame.Server.Tests;

public sealed class AccountManageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    [Fact]
    public async Task Manage_and_delete_require_sign_in()
    {
        using var client = factory.CreateFormClient();

        foreach (var path in new[] { "/Account/Manage", "/Account/Delete" })
        {
            var (response, _) = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var location = Uri.UnescapeDataString(response.Headers.Location?.OriginalString ?? "");
            Assert.Contains("/Account/Login", location, StringComparison.Ordinal);
            Assert.Contains("ReturnUrl=" + path, location, StringComparison.Ordinal);
        }
    }

    [Fact] // AK-13b
    public async Task Lists_passkeys_of_the_account()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var second = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email, new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero));
        var first = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email, new DateTimeOffset(2026, 1, 2, 12, 0, 0, TimeSpan.Zero));

        var (_, html) = await client.GetAsync("/Account/Manage");

        Assert.Contains($"value=\"{AccountTestFlows.PasskeyId(first)}\"", html, StringComparison.Ordinal);
        Assert.Contains($"value=\"{AccountTestFlows.PasskeyId(second)}\"", html, StringComparison.Ordinal);
        var text = HtmlTexts.Visible(html);
        Assert.Contains("Passkey 1, angelegt am 2026-01-02", text, StringComparison.Ordinal);
        Assert.Contains("Passkey 2, angelegt am 2026-01-05", text, StringComparison.Ordinal);
    }

    [Fact] // AK-13b
    public async Task Removes_a_passkey_when_password_remains()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var id = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);

        var (_, html) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(id));

        Assert.Contains("Passkey entfernt.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Equal(0, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact] // AK-13c
    public async Task Last_credential_cannot_be_removed()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var id = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);
        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);

        var (_, page) = await client.GetAsync("/Account/Manage");
        Assert.DoesNotContain("handler=RemovePasskey", page, StringComparison.Ordinal);
        Assert.Contains("Deine einzige Anmeldeart kannst du nicht entfernen.", HtmlTexts.Visible(page), StringComparison.Ordinal);

        var (_, html) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(id));

        Assert.Contains("Das ist deine einzige Anmeldeart.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact] // AK-13c
    public async Task Passkey_only_with_two_can_remove_one_but_not_the_last()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var first = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email, DateTimeOffset.UtcNow.AddDays(-1));
        var second = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);
        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);

        var (_, removed) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(first));
        Assert.Contains("Passkey entfernt.", HtmlTexts.Visible(removed), StringComparison.Ordinal);
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));

        var (_, blocked) = await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(second));
        Assert.Contains("Das ist deine einzige Anmeldeart.", HtmlTexts.Visible(blocked), StringComparison.Ordinal);
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact] // AK-13b
    public async Task Cannot_remove_passkey_of_another_account()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var other = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var foreign = await AccountTestFlows.AddStoredPasskeyAsync(factory, other.Email);
        await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);

        await AccountTestFlows.RemovePasskeyAsync(client, AccountTestFlows.PasskeyId(foreign));

        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, other.Email));
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact] // AK-13b
    public async Task Passkey_only_account_can_set_password()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);
        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);
        var (_, page) = await client.GetAsync("/Account/Manage");
        Assert.Contains("handler=SetPassword", page, StringComparison.Ordinal);

        var (response, html) = await AccountTestFlows.SetPasswordAsync(client, "ein-ganz-neues-passwort");

        Assert.Contains("Passwort gesetzt.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));
        var expires = DateTimeOffset.Parse(
            cookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange((expires - DateTimeOffset.UtcNow).TotalDays, 29, 31);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);
        using var fresh = factory.CreateFormClient();
        var (login, _) = await AccountTestFlows.LoginAsync(fresh, account.Email, "ein-ganz-neues-passwort");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/play", login.Headers.Location?.OriginalString);
    }

    [Fact] // AK-13b
    public async Task Set_password_validates()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);
        await AccountTestFlows.RemovePasswordDirectlyAsync(factory, account.Email);

        var (_, tooShort) = await AccountTestFlows.SetPasswordAsync(client, "kurz");
        var (_, mismatch) = await AccountTestFlows.SetPasswordAsync(client, "ein-ganz-neues-passwort", "etwas-ganz-anderes");
        var (_, empty) = await AccountTestFlows.SetPasswordAsync(client, "", "");

        Assert.Contains("mindestens 10 Zeichen", HtmlTexts.Visible(tooShort), StringComparison.Ordinal);
        Assert.Contains("stimmen nicht überein", HtmlTexts.Visible(mismatch), StringComparison.Ordinal);
        Assert.Contains("Bitte ein neues Passwort angeben.", HtmlTexts.Visible(empty), StringComparison.Ordinal);
        Assert.Contains("Bitte das Passwort wiederholen.", HtmlTexts.Visible(empty), StringComparison.Ordinal);
        var (_, page) = await client.GetAsync("/Account/Manage");
        Assert.Contains("handler=SetPassword", page, StringComparison.Ordinal);
    }

    [Fact] // AK-13b
    public async Task Set_password_is_ignored_when_account_has_one()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;

        await AccountTestFlows.SetPasswordAsync(client, "ein-ganz-neues-passwort");

        using var fresh = factory.CreateFormClient();
        var (login, _) = await AccountTestFlows.LoginAsync(fresh, account.Email);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        var (other, _) = await AccountTestFlows.LoginAsync(fresh, account.Email, "ein-ganz-neues-passwort");
        Assert.Equal(HttpStatusCode.OK, other.StatusCode);
    }

    [Fact]
    public async Task Manage_offers_sign_out_and_delete()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;

        var (_, html) = await client.GetAsync("/Account/Manage");

        Assert.Contains("action=\"/Account/Logout\"", html, StringComparison.Ordinal);
        Assert.Contains("href=\"/Account/Delete\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Home_links_to_manage_when_signed_in()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;

        var (_, html) = await client.GetAsync("/");

        Assert.Contains("href=\"/Account/Manage\"", html, StringComparison.Ordinal);
        Assert.Contains("Mein Account", HtmlTexts.Visible(html), StringComparison.Ordinal);
    }

    [Fact]
    public void Manage_and_delete_are_rate_limited()
    {
        foreach (var model in new[] { typeof(ManageModel), typeof(DeleteModel) })
        {
            Assert.Equal(WebDefaults.AuthRateLimitPolicy, model.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }
    }
}
