using System.Net;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>003 T9b: Anmeldung mit Passkey über die Login-Seite.</summary>
public sealed class PasskeyLoginTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static bool HasSession(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));

    private static async Task<string> PostPasskeyAsync(FormClient client, string credential)
    {
        var (_, page) = await client.GetAsync("/Account/Login");
        var (response, html) = await client.PostWithTokenAsync(
            "/Account/Login?handler=Passkey", new Dictionary<string, string> { ["Credential"] = credential }, FormClient.AntiforgeryToken(page));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasSession(response));
        return html;
    }

    /// <summary>Account mit Passwort und einem Passkey des Authenticators; der Client ist danach abgemeldet.</summary>
    private async Task<AccountTestFlows.CreatedAccount> AccountWithPasskeyAsync(SoftwareAuthenticator authenticator, string? language = null)
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory, language);
        using var _ = client;
        await AccountTestFlows.AddPasskeyAsync(client, authenticator);
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
        return account;
    }

    [Fact] // AK-4
    public async Task Login_page_renders_discoverable_request_options()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await client.GetAsync("/Account/Login");

        Assert.Contains("data-passkey=\"get\"", html, StringComparison.Ordinal);
        Assert.Contains("handler=Passkey", html, StringComparison.Ordinal);
        Assert.Contains("\"challenge\"", AccountTestFlows.PasskeyOptions(html), StringComparison.Ordinal);
    }

    [Fact] // AK-4
    public async Task Failed_password_login_still_offers_passkey()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await AccountTestFlows.LoginAsync(client, AccountTestFlows.UniqueEmail());

        Assert.Contains("data-passkey=\"get\"", html, StringComparison.Ordinal);
        Assert.Contains("\"challenge\"", AccountTestFlows.PasskeyOptions(html), StringComparison.Ordinal);
    }

    [Fact] // AK-9
    public async Task Garbage_or_empty_credential_fails_without_session()
    {
        using var client = factory.CreateFormClient();

        foreach (var credential in new[] { "", "{}", "kein json", "{\"id\":\"x\",\"type\":\"public-key\",\"response\":{}}" })
        {
            var html = await PostPasskeyAsync(client, credential);

            Assert.Contains("Die Anmeldung mit Passkey ist fehlgeschlagen.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Passkey_post_without_pending_ceremony_fails_cleanly()
    {
        using var authenticator = new SoftwareAuthenticator();
        await AccountWithPasskeyAsync(authenticator);
        using var other = factory.CreateFormClient();
        var (_, page) = await other.GetAsync("/Account/Login");
        var credential = authenticator.Get(AccountTestFlows.PasskeyOptions(page));
        using var client = factory.CreateFormClient();
        var (_, register) = await client.GetAsync("/Account/Register");

        var (response, html) = await client.PostWithTokenAsync(
            "/Account/Login?handler=Passkey", new Dictionary<string, string> { ["Credential"] = credential }, FormClient.AntiforgeryToken(register));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasSession(response));
        Assert.Contains("Die Anmeldung mit Passkey ist fehlgeschlagen.", HtmlTexts.Visible(html), StringComparison.Ordinal);
    }

    [Fact] // AK-4, AK-11, AK-13b
    public async Task Added_passkey_signs_in_persistently_and_returns()
    {
        using var authenticator = new SoftwareAuthenticator();
        await AccountWithPasskeyAsync(authenticator);
        using var client = factory.CreateFormClient();

        var (response, _) = await AccountTestFlows.PasskeyLoginAsync(client, authenticator, "/play");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/play", response.Headers.Location?.OriginalString);
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));
        var expires = DateTimeOffset.Parse(
            cookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange((expires - DateTimeOffset.UtcNow).TotalDays, 29, 31);
        var (play, _) = await client.GetAsync("/play");
        Assert.Equal(HttpStatusCode.OK, play.StatusCode);
    }

    [Fact] // AK-4
    public async Task Unsafe_return_url_is_ignored()
    {
        using var authenticator = new SoftwareAuthenticator();
        await AccountWithPasskeyAsync(authenticator);
        using var client = factory.CreateFormClient();

        var (response, _) = await AccountTestFlows.PasskeyLoginAsync(client, authenticator, "https://evil.example/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.DoesNotContain("evil.example", response.Headers.Location?.OriginalString, StringComparison.Ordinal);
    }

    [Fact] // 006 AK-6
    public async Task Passkey_login_sets_account_language_cookie()
    {
        using var authenticator = new SoftwareAuthenticator();
        await AccountWithPasskeyAsync(authenticator, "en");
        using var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de");

        var (response, _) = await AccountTestFlows.PasskeyLoginAsync(client, authenticator);

        var culture = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".AspNetCore.Culture=", StringComparison.Ordinal));
        Assert.Equal("c=en|uic=en", WebUtility.UrlDecode(culture.Split(';')[0][".AspNetCore.Culture=".Length..]));
    }

    [Fact] // AK-10
    public async Task Locked_out_account_cannot_sign_in_with_passkey()
    {
        using var authenticator = new SoftwareAuthenticator();
        var account = await AccountWithPasskeyAsync(authenticator);
        using var client = factory.CreateFormClient();
        for (var i = 0; i < 5; i++)
        {
            await AccountTestFlows.LoginAsync(client, account.Email, "falsches-passwort");
        }

        var (response, html) = await AccountTestFlows.PasskeyLoginAsync(client, authenticator);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasSession(response));
        Assert.Contains("Die Anmeldung mit Passkey ist fehlgeschlagen.", HtmlTexts.Visible(html), StringComparison.Ordinal);
    }

    [Fact] // AK-3
    public async Task Unconfirmed_account_gets_confirm_message()
    {
        using var authenticator = new SoftwareAuthenticator();
        var account = await AccountWithPasskeyAsync(authenticator);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var user = (await users.FindByEmailAsync(account.Email))!;
            user.EmailConfirmed = false;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }

        using var client = factory.CreateFormClient();
        var (response, html) = await AccountTestFlows.PasskeyLoginAsync(client, authenticator);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(HasSession(response));
        Assert.Contains("Bitte bestätige zuerst deine E-Mail-Adresse.", HtmlTexts.Visible(html), StringComparison.Ordinal);
    }
}
