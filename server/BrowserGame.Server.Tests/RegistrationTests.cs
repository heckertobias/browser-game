using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using static BrowserGame.Server.Tests.HtmlTexts;

namespace BrowserGame.Server.Tests;

/// <summary>003 T9c: Registrierung in zwei Schritten – E-Mail-Link, dann Name und Passwort oder Passkey.</summary>
public sealed partial class RegistrationTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string InvalidText = "Der Link ist ungültig oder abgelaufen.";
    private const string PasskeyFailedText = "Der Passkey konnte nicht angelegt werden.";

    private async Task<Account?> FindByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
    }

    private async Task<Account?> FindByNameAsync(string name)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByNameAsync(name);
    }

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));

    private static void AssertSessionFor30Days(HttpResponseMessage response)
    {
        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));
        var expires = DateTimeOffset.Parse(
            cookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            CultureInfo.InvariantCulture);
        Assert.InRange((expires - DateTimeOffset.UtcNow).TotalDays, 29, 31);
    }

    private static string Normalize(string html) => TokenPattern().Replace(html, "");

    private async Task<string> RequestLinkAsync(FormClient client, string email)
    {
        await AccountTestFlows.RequestRegistrationAsync(client, email);
        return AccountTestFlows.RegistrationLink(factory, email);
    }

    private static FormClient NoRedirectClient(WebApplicationFactory<Program> app) =>
        new(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

    [Fact] // AK-2, AK-3
    public async Task Step_one_sends_a_link_and_creates_nothing()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();

        var (response, html) = await AccountTestFlows.RequestRegistrationAsync(client, email);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Visible(html);
        Assert.Contains("Fast geschafft!", text, StringComparison.Ordinal);
        Assert.Contains("30 Minuten gültig", text, StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Equal("Registrierung abschließen", mail.Subject);
        Assert.StartsWith("Hallo,", mail.Text, StringComparison.Ordinal);
        Assert.Contains("30 Minuten gültig", mail.Text, StringComparison.Ordinal);
        Assert.Matches(@"^/Account/CompleteRegistration\?code=[A-Za-z0-9_-]+$", AccountTestFlows.LinkFromMail(mail.Text));
        var (login, _) = await AccountTestFlows.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.False(HasSessionCookie(login));
    }

    [Fact] // AK-3, AK-9
    public async Task Step_one_answers_the_same_for_new_and_existing_address()
    {
        using var client = factory.CreateFormClient();
        var existing = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var fresh = AccountTestFlows.UniqueEmail();

        var (_, forExisting) = await AccountTestFlows.RequestRegistrationAsync(client, existing.Email);
        var (_, forNew) = await AccountTestFlows.RequestRegistrationAsync(client, fresh);

        Assert.Equal(Normalize(forNew), Normalize(forExisting));
        Assert.Equal("Du hast schon einen Account", Assert.Single(factory.Mails.To(existing.Email)).Subject);
        Assert.Equal("Registrierung abschließen", Assert.Single(factory.Mails.To(fresh)).Subject);
        Assert.Equal(existing.Name, (await FindByEmailAsync(existing.Email))?.UserName);
    }

    [Fact] // AK-2
    public async Task Invalid_email_is_rejected_without_mail()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await AccountTestFlows.RequestRegistrationAsync(client, "kein-at");

        var text = Visible(html);
        Assert.Contains("Das ist keine gültige E-Mail-Adresse.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Fast geschafft", text, StringComparison.Ordinal);
        Assert.Empty(factory.Mails.To("kein-at"));
    }

    [Fact] // AK-2, AK-6, AK-11
    public async Task Password_registration_creates_a_confirmed_account_and_signs_in()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var link = await RequestLinkAsync(client, email);

        var (_, intro) = await client.GetAsync(link);
        Assert.Contains(email, Visible(intro), StringComparison.Ordinal);
        Assert.Contains("name=\"Name\"", intro, StringComparison.Ordinal);
        var (_, chosen) = await AccountTestFlows.ChooseNameAsync(client, link, name.ToUpperInvariant());
        Assert.Contains($"Dein Spielername: {name}", Visible(chosen), StringComparison.Ordinal);
        Assert.Contains("data-passkey=\"create\"", chosen, StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));

        var (response, _) = await AccountTestFlows.CompleteWithPasswordAsync(client, link, name);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/play", response.Headers.Location?.OriginalString);
        AssertSessionFor30Days(response);
        var account = await FindByEmailAsync(email);
        Assert.NotNull(account);
        Assert.Equal(name, account.UserName);
        Assert.True(account.EmailConfirmed);
        Assert.Equal("de", account.Language);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().HasPasswordAsync(account));
        }

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);
        using var fresh = factory.CreateFormClient();
        Assert.Equal(HttpStatusCode.Redirect, (await AccountTestFlows.LoginAsync(fresh, email)).Response.StatusCode);
    }

    [Fact] // AK-2, AK-4, AK-11
    public async Task Passkey_registration_creates_a_passwordless_account_that_signs_in_with_the_passkey()
    {
        using var authenticator = new SoftwareAuthenticator();
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var link = await RequestLinkAsync(client, email);

        var (response, _) = await AccountTestFlows.CompleteWithPasskeyAsync(client, link, name, authenticator);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/play", response.Headers.Location?.OriginalString);
        AssertSessionFor30Days(response);
        var account = await FindByEmailAsync(email);
        Assert.NotNull(account);
        Assert.True(account.EmailConfirmed);
        Assert.Equal(name, account.UserName);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.False(await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().HasPasswordAsync(account));
        }

        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, email));
        using var fresh = factory.CreateFormClient();
        var (login, _) = await AccountTestFlows.PasskeyLoginAsync(fresh, authenticator);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/play", login.Headers.Location?.OriginalString);
    }

    [Fact] // AK-4
    public async Task Failed_passkey_keeps_the_step_and_creates_nothing()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var link = await RequestLinkAsync(client, email);
        var (_, chosen) = await AccountTestFlows.ChooseNameAsync(client, link, name);

        var (response, html) = await client.PostWithTokenAsync(
            link,
            new Dictionary<string, string> { ["Step"] = "passkey", ["Name"] = name, ["Credential"] = "{}" },
            FormClient.AntiforgeryToken(chosen));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = Visible(html);
        Assert.Contains(PasskeyFailedText, text, StringComparison.Ordinal);
        Assert.Contains("Dein Spielername", text, StringComparison.Ordinal);
        Assert.Contains("\"challenge\"", AccountTestFlows.PasskeyOptions(html), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
        Assert.False(HasSessionCookie(response));
    }

    [Fact] // AK-4
    public async Task Passkey_created_for_another_link_is_rejected()
    {
        using var authenticator = new SoftwareAuthenticator();
        using var client = factory.CreateFormClient();
        var emailA = AccountTestFlows.UniqueEmail();
        var emailB = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var linkA = await RequestLinkAsync(client, emailA);
        var linkB = await RequestLinkAsync(client, emailB);
        var (_, chosen) = await AccountTestFlows.ChooseNameAsync(client, linkA, name);

        var (_, html) = await client.PostWithTokenAsync(
            linkB,
            new Dictionary<string, string>
            {
                ["Step"] = "passkey",
                ["Name"] = name,
                ["Credential"] = authenticator.Create(AccountTestFlows.PasskeyOptions(chosen)),
            },
            FormClient.AntiforgeryToken(chosen));

        Assert.Contains(PasskeyFailedText, Visible(html), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(emailA));
        Assert.Null(await FindByEmailAsync(emailB));
    }

    [Fact] // AK-3
    public async Task Used_link_is_worthless()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(factory, client, email, AccountTestFlows.UniqueName());
        var link = AccountTestFlows.RegistrationLink(factory, email);
        var other = AccountTestFlows.UniqueName();
        using var second = factory.CreateFormClient();

        var (_, page) = await second.GetAsync(link);
        var (_, posted) = await AccountTestFlows.CompleteWithPasswordAsync(second, link, other);

        Assert.Contains(InvalidText, Visible(page), StringComparison.Ordinal);
        Assert.Contains(InvalidText, Visible(posted), StringComparison.Ordinal);
        Assert.Null(await FindByNameAsync(other));
    }

    [Fact] // AK-3
    public async Task Expired_link_is_rejected_and_registering_again_works()
    {
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting("Registration:LinkLifetimeMinutes", "-1"));
        using var expiring = NoRedirectClient(app);
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        await AccountTestFlows.RequestRegistrationAsync(expiring, email);
        var link = AccountTestFlows.RegistrationLink(factory, email);

        var (_, page) = await expiring.GetAsync(link);
        var (_, posted) = await AccountTestFlows.CompleteWithPasswordAsync(expiring, link, name);

        Assert.Contains(InvalidText, Visible(page), StringComparison.Ordinal);
        Assert.Contains(InvalidText, Visible(posted), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));

        using var again = factory.CreateFormClient();
        var (response, _) = await AccountTestFlows.RegisterAsync(factory, again, email, name);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.NotNull(await FindByEmailAsync(email));
    }

    [Fact] // AK-3
    public async Task Link_lifetime_follows_the_setting()
    {
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting("Registration:LinkLifetimeMinutes", "45"));
        using var client = NoRedirectClient(app);
        var email = AccountTestFlows.UniqueEmail();

        var (_, html) = await AccountTestFlows.RequestRegistrationAsync(client, email);

        Assert.Contains("45 Minuten gültig", Visible(html), StringComparison.Ordinal);
        Assert.Contains("45 Minuten gültig", Assert.Single(factory.Mails.To(email)).Text, StringComparison.Ordinal);
    }

    [Fact] // AK-3
    public async Task Changed_or_missing_code_is_rejected()
    {
        using var client = factory.CreateFormClient();
        var valid = await RequestLinkAsync(client, AccountTestFlows.UniqueEmail());
        var name = AccountTestFlows.UniqueName();

        foreach (var url in new[] { "/Account/CompleteRegistration", "/Account/CompleteRegistration?code=kaputt", valid[..^2] })
        {
            var (_, html) = await client.GetAsync(url);
            Assert.Contains(InvalidText, Visible(html), StringComparison.Ordinal);
        }

        var (_, posted) = await AccountTestFlows.CompleteWithPasswordAsync(client, "/Account/CompleteRegistration?code=kaputt", name);
        Assert.Contains(InvalidText, Visible(posted), StringComparison.Ordinal);
        Assert.Null(await FindByNameAsync(name));
    }

    [Fact] // AK-7
    public async Task Taken_name_is_rejected_regardless_of_spelling()
    {
        using var client = factory.CreateFormClient();
        var first = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var email = AccountTestFlows.UniqueEmail();
        var link = await RequestLinkAsync(client, email);

        var (_, html) = await AccountTestFlows.ChooseNameAsync(client, link, first.Name.ToUpperInvariant());

        Assert.Contains("ist schon vergeben", Visible(html), StringComparison.Ordinal);
        Assert.DoesNotContain("data-passkey=\"create\"", html, StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact] // AK-7
    public async Task Name_taken_in_the_meantime_is_rejected_at_the_end()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var link = await RequestLinkAsync(client, email);
        var (_, chosen) = await AccountTestFlows.ChooseNameAsync(client, link, name);
        Assert.Contains("data-passkey=\"create\"", chosen, StringComparison.Ordinal);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            var created = await users.CreateAsync(
                new Account { UserName = name, Email = AccountTestFlows.UniqueEmail(), EmailConfirmed = true }, AccountTestFlows.Password);
            Assert.True(created.Succeeded);
        }

        var (_, html) = await AccountTestFlows.CompleteWithPasswordAsync(client, link, name);

        Assert.Contains("ist schon vergeben", Visible(html), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Theory] // AK-5, AK-7b
    [InlineData("Ab1", "nur aus den Buchstaben")]
    [InlineData("Al", "3 bis 16 Zeichen lang")]
    [InlineData("aDmIn", "Dieser Name ist nicht erlaubt.")]
    public async Task Invalid_or_blocked_name_is_rejected_with_reason(string name, string reason)
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var link = await RequestLinkAsync(client, email);

        var (_, html) = await AccountTestFlows.ChooseNameAsync(client, link, name);

        Assert.Contains(reason, Visible(html), StringComparison.Ordinal);
        Assert.DoesNotContain("data-passkey=\"create\"", html, StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact] // AK-2
    public async Task Short_or_mismatched_password_is_rejected_in_step_two()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();
        var link = await RequestLinkAsync(client, email);

        var (_, tooShort) = await AccountTestFlows.CompleteWithPasswordAsync(client, link, name, "kurz");
        var (_, mismatch) = await AccountTestFlows.CompleteWithPasswordAsync(client, link, name, confirm: "anders-passwort");

        Assert.Contains("mindestens 10 Zeichen", Visible(tooShort), StringComparison.Ordinal);
        Assert.Contains("Dein Spielername", Visible(tooShort), StringComparison.Ordinal);
        Assert.Contains("stimmen nicht überein", Visible(mismatch), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact] // AK-5
    public async Task Empty_forms_show_one_message_per_field()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var link = await RequestLinkAsync(client, email);

        var (_, step1) = await AccountTestFlows.RequestRegistrationAsync(client, "");
        var (_, noName) = await AccountTestFlows.ChooseNameAsync(client, link, "");
        var (_, noPassword) = await AccountTestFlows.CompleteWithPasswordAsync(client, link, AccountTestFlows.UniqueName(), "", "");

        Assert.Equal(1, Occurrences(WebUtility.HtmlDecode(step1), "<li>Bitte E-Mail angeben."));
        Assert.Equal(1, Occurrences(WebUtility.HtmlDecode(noName), "<li>Bitte einen Spielernamen angeben."));
        var password = WebUtility.HtmlDecode(noPassword);
        Assert.Equal(1, Occurrences(password, "<li>Bitte Passwort angeben."));
        Assert.Equal(1, Occurrences(password, "<li>Bitte das Passwort wiederholen."));
        foreach (var text in new[] { step1, noName, password })
        {
            Assert.DoesNotContain("Zeichen lang", text, StringComparison.Ordinal);
            Assert.DoesNotContain("stimmen nicht überein", text, StringComparison.Ordinal);
        }
    }

    [Fact] // AK-3
    public async Task Language_switch_on_the_name_step_returns_to_the_same_link()
    {
        using var client = factory.CreateFormClient();
        var link = await RequestLinkAsync(client, AccountTestFlows.UniqueEmail());
        var name = AccountTestFlows.UniqueName();
        var (_, chosen) = await AccountTestFlows.ChooseNameAsync(client, link, name);
        var returnUrl = WebUtility.HtmlDecode(ReturnUrlPattern().Match(chosen).Groups[1].Value);
        Assert.Equal(link, returnUrl);

        var (switched, _) = await client.PostWithTokenAsync(
            "/Language", new Dictionary<string, string> { ["Language"] = "en", ["ReturnUrl"] = returnUrl }, FormClient.AntiforgeryToken(chosen));
        Assert.Equal(link, switched.Headers.Location?.OriginalString);
        var (_, html) = await client.GetAsync(switched.Headers.Location!.OriginalString);

        var text = Visible(html);
        Assert.Contains("First choose your player name.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("The link is invalid", text, StringComparison.Ordinal);
    }

    private static int Occurrences(string text, string part) => text.Split(part, StringSplitOptions.None).Length - 1;

    [GeneratedRegex("name=\"ReturnUrl\" value=\"([^\"]*)\"")]
    private static partial Regex ReturnUrlPattern();

    [GeneratedRegex("<input name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenPattern();
}
