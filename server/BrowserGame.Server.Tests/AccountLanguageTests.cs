using System.Net;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>006 T6: Registrierung speichert die Sprache, Anmeldung setzt das Culture-Cookie aus dem Account.</summary>
public sealed class AccountLanguageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string CultureCookie = ".AspNetCore.Culture";

    private FormClient ClientWith(string acceptLanguage)
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return client;
    }

    private static string? CultureCookieValue(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split(';')[0]).Where(v => v.StartsWith(CultureCookie + "=", StringComparison.Ordinal))
                .Select(v => WebUtility.UrlDecode(v[(CultureCookie.Length + 1)..])).SingleOrDefault()
            : null;

    private async Task<AccountTestFlows.CreatedAccount> CreateAccountAsync(string language, bool confirmed = true)
    {
        var account = new AccountTestFlows.CreatedAccount(AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var result = await users.CreateAsync(
            new Account { UserName = account.Name, Email = account.Email, EmailConfirmed = confirmed, Language = language }, AccountTestFlows.Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    private async Task<string?> StoredLanguageAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        return (await users.FindByEmailAsync(email))?.Language;
    }

    private static async Task SwitchAsync(FormClient client, string language) =>
        await client.PostAsync("/", "/Language", new Dictionary<string, string> { ["Language"] = language, ["ReturnUrl"] = "/" });

    private static void AssertPage(string html, string lang, string text)
    {
        Assert.Contains($"<html lang=\"{lang}\"", html, StringComparison.Ordinal);
        Assert.Contains(text, WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Theory] // AK-7
    [InlineData("en", "en")]
    [InlineData("de", "de")]
    [InlineData("de-AT", "de")]
    [InlineData("fr", "en")]
    [InlineData("fr, de", "en")]
    public async Task Registration_saves_the_language_of_the_request(string acceptLanguage, string expected)
    {
        using var client = ClientWith(acceptLanguage);
        var email = AccountTestFlows.UniqueEmail();

        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());

        Assert.Equal(expected, await StoredLanguageAsync(email));
    }

    [Fact] // AK-7
    public async Task Registration_follows_the_culture_cookie()
    {
        using var client = ClientWith("de");
        await SwitchAsync(client, "en");
        var email = AccountTestFlows.UniqueEmail();

        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());

        Assert.Equal("en", await StoredLanguageAsync(email));
    }

    [Theory] // AK-7
    [InlineData("en", "de", "Fast geschafft")]
    [InlineData("de", "en", "Almost done")]
    public async Task Registering_an_existing_email_keeps_the_owner_language(string ownerLanguage, string clientLanguage, string expectedText)
    {
        var owner = await CreateAccountAsync(ownerLanguage);
        using var client = ClientWith(clientLanguage);
        var name = AccountTestFlows.UniqueName();

        var (response, html) = await AccountTestFlows.RegisterAsync(client, owner.Email, name);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPage(html, clientLanguage, expectedText);
        Assert.Equal(ownerLanguage, await StoredLanguageAsync(owner.Email));
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        Assert.Null(await users.FindByNameAsync(name));
        Assert.Single(factory.Mails.To(owner.Email));
    }

    [Fact] // AK-6
    public async Task Sign_in_sets_the_culture_cookie_from_the_account()
    {
        var english = await CreateAccountAsync("en");
        using var enClient = ClientWith("de");

        var (login, _) = await AccountTestFlows.LoginAsync(enClient, english.Email);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/play", login.Headers.Location?.OriginalString);
        Assert.Equal("c=en|uic=en", CultureCookieValue(login));
        AssertPage((await enClient.GetAsync("/")).Html, "en", "Signed in as");

        var german = await CreateAccountAsync("de");
        using var deClient = ClientWith("en");

        var (loginDe, _) = await AccountTestFlows.LoginAsync(deClient, german.Email);

        Assert.Equal("c=de|uic=de", CultureCookieValue(loginDe));
        AssertPage((await deClient.GetAsync("/")).Html, "de", "Angemeldet als");
    }

    [Fact] // AK-6
    public async Task Account_language_wins_over_an_existing_cookie()
    {
        var account = await CreateAccountAsync("de");
        using var client = ClientWith("de");
        await SwitchAsync(client, "en");

        var (login, _) = await AccountTestFlows.LoginAsync(client, account.Email);

        Assert.Equal("c=de|uic=de", CultureCookieValue(login));
        AssertPage((await client.GetAsync("/")).Html, "de", "Angemeldet als");
        Assert.Equal("de", await StoredLanguageAsync(account.Email));
    }

    [Fact] // AK-6
    public async Task Language_switched_on_one_device_applies_on_sign_in_on_another()
    {
        var account = await CreateAccountAsync("de");
        using var first = ClientWith("de");
        await AccountTestFlows.LoginAsync(first, account.Email);
        await SwitchAsync(first, "en");
        using var second = ClientWith("de");

        await AccountTestFlows.LoginAsync(second, account.Email);

        AssertPage((await second.GetAsync("/")).Html, "en", "Signed in as");
    }

    [Fact] // AK-6
    public async Task Failed_sign_in_sets_no_culture_cookie()
    {
        var account = await CreateAccountAsync("en");
        var unconfirmed = await CreateAccountAsync("en", confirmed: false);
        var locked = await CreateAccountAsync("en");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var user = (await users.FindByEmailAsync(locked.Email))!;
            await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddHours(1));
        }

        var attempts = new[]
        {
            (account.Email, "falsches-passwort"),
            (AccountTestFlows.UniqueEmail(), AccountTestFlows.Password),
            (unconfirmed.Email, AccountTestFlows.Password),
            (locked.Email, AccountTestFlows.Password),
            ("", ""),
        };

        foreach (var (email, password) in attempts)
        {
            using var client = ClientWith("de");

            var (response, html) = await AccountTestFlows.LoginAsync(client, email, password);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Null(CultureCookieValue(response));
            Assert.Contains("<html lang=\"de\"", html, StringComparison.Ordinal);
        }
    }

    [Fact] // AK-6
    public async Task Culture_cookie_stays_after_sign_out()
    {
        var account = await CreateAccountAsync("en");
        using var client = ClientWith("de");
        await AccountTestFlows.LoginAsync(client, account.Email);

        var (logout, _) = await client.PostAsync("/", "/Account/Logout", new Dictionary<string, string>());

        Assert.Null(CultureCookieValue(logout));
        AssertPage((await client.GetAsync("/")).Html, "en", "Sign in");
    }

    [Fact] // AK-6, AK-7
    public async Task Registered_language_applies_on_another_device()
    {
        using var first = ClientWith("en");
        var email = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(first, email, AccountTestFlows.UniqueName());
        var link = AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(email)).Text);
        await first.GetAsync(link);
        using var second = ClientWith("de");

        var (login, _) = await AccountTestFlows.LoginAsync(second, email);

        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        AssertPage((await second.GetAsync("/")).Html, "en", "Signed in as");
    }
}
