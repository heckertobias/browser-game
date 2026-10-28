using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

public sealed partial class AccountPagesTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly string[] SessionCookieOnly = ["bg.auth"];

    [Fact] // AK-2, AK-6
    public async Task Registration_creates_account_with_normalized_name_and_sends_confirmation_mail()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();

        var (response, html) = await AccountTestFlows.RegisterAsync(client, email, name.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Fast geschafft", html, StringComparison.Ordinal);
        var account = await FindByEmailAsync(email);
        Assert.NotNull(account);
        Assert.Equal(name, account.UserName);
        Assert.False(account.EmailConfirmed);
        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Matches(@"/Account/ConfirmEmail\?userId=[^&\s]+&code=\S+", AccountTestFlows.LinkFromMail(mail.Text));
    }

    [Fact] // AK-3
    public async Task Unconfirmed_account_cannot_play_until_link_is_clicked()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());

        var (login, loginHtml) = await AccountTestFlows.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        AssertGenericError(loginHtml);
        Assert.False(HasSessionCookie(login));
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/play")).Response.StatusCode);

        await client.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", new Dictionary<string, string> { ["Email"] = email });
        var mails = factory.Mails.To(email);
        Assert.Equal(2, mails.Count);

        var (confirm, confirmHtml) = await client.GetAsync(AccountTestFlows.LinkFromMail(mails[1].Text));
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        Assert.Contains("bestätigt", WebUtility.HtmlDecode(confirmHtml), StringComparison.Ordinal);

        var (signedIn, _) = await AccountTestFlows.LoginAsync(client, email);
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Equal("/play", signedIn.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);
    }

    [Fact] // AK-3
    public async Task Invalid_confirmation_link_is_rejected()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var user = await FindByEmailAsync(account.Email);

        var (_, html) = await client.GetAsync($"/Account/ConfirmEmail?userId={user!.Id}&code=kaputt");

        Assert.Contains("ungültig oder abgelaufen", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-3, AK-9
    public async Task Resend_answers_the_same_and_sends_nothing_for_unknown_or_confirmed_address()
    {
        using var client = factory.CreateFormClient();
        var confirmed = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var unknown = AccountTestFlows.UniqueEmail();

        var (_, confirmedHtml) = await client.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", new Dictionary<string, string> { ["Email"] = confirmed.Email });
        var (_, unknownHtml) = await client.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", new Dictionary<string, string> { ["Email"] = unknown });

        Assert.Contains("Falls es zu dieser Adresse", WebUtility.HtmlDecode(confirmedHtml), StringComparison.Ordinal);
        Assert.Equal(Normalize(confirmedHtml, confirmed.Email), Normalize(unknownHtml, unknown));
        Assert.Empty(factory.Mails.To(confirmed.Email));
        Assert.Empty(factory.Mails.To(unknown));
    }

    [Fact] // AK-7
    public async Task Taken_name_is_rejected_regardless_of_spelling()
    {
        using var client = factory.CreateFormClient();
        var first = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var email = AccountTestFlows.UniqueEmail();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, email, first.Name.ToUpperInvariant());

        Assert.Contains("ist schon vergeben", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
        Assert.Empty(factory.Mails.To(email));
    }

    [Fact] // AK-9
    public async Task Failed_logins_look_identical()
    {
        using var client = factory.CreateFormClient();
        var confirmed = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var unconfirmedEmail = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(client, unconfirmedEmail, AccountTestFlows.UniqueName());
        var unknown = AccountTestFlows.UniqueEmail();

        var wrongPassword = await AccountTestFlows.LoginAsync(client, confirmed.Email, "falsches-passwort");
        var unknownEmail = await AccountTestFlows.LoginAsync(client, unknown);
        var unconfirmed = await AccountTestFlows.LoginAsync(client, unconfirmedEmail);

        foreach (var (response, html) in new[] { wrongPassword, unknownEmail, unconfirmed })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertGenericError(html);
            Assert.False(HasSessionCookie(response));
        }

        var expected = Normalize(wrongPassword.Html, confirmed.Email);
        Assert.Equal(expected, Normalize(unknownEmail.Html, unknown));
        Assert.Equal(expected, Normalize(unconfirmed.Html, unconfirmedEmail));
    }

    [Fact] // AK-2, AK-9
    public async Task Registering_an_existing_email_looks_like_success_and_warns_the_owner()
    {
        using var client = factory.CreateFormClient();
        var existing = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var name = AccountTestFlows.UniqueName();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, existing.Email, name);

        Assert.Contains("Fast geschafft", html, StringComparison.Ordinal);
        Assert.Equal("Du hast schon einen Account", Assert.Single(factory.Mails.To(existing.Email)).Subject);
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByNameAsync(name));
    }

    [Fact] // AK-9
    public async Task Short_password_is_reported_for_new_and_existing_email_alike()
    {
        using var client = factory.CreateFormClient();
        var existing = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        var (_, forNew) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName(), "kurz");
        var (_, forExisting) = await AccountTestFlows.RegisterAsync(client, existing.Email, AccountTestFlows.UniqueName(), "kurz");

        Assert.Contains("mindestens 10 Zeichen", WebUtility.HtmlDecode(forNew), StringComparison.Ordinal);
        Assert.Contains("mindestens 10 Zeichen", WebUtility.HtmlDecode(forExisting), StringComparison.Ordinal);
        Assert.Empty(factory.Mails.To(existing.Email));
    }

    [Fact] // AK-5
    public async Task Invalid_name_is_rejected_with_reason()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, email, "Ab1");

        Assert.Contains("nur aus den Buchstaben", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Null(await FindByEmailAsync(email));
    }

    [Fact] // AK-5
    public async Task Empty_form_shows_one_message_per_field()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, "", "", "", "");

        var text = WebUtility.HtmlDecode(html);
        foreach (var message in new[] { "Bitte E-Mail angeben.", "Bitte einen Spielernamen angeben.", "Bitte Passwort angeben.", "Bitte das Passwort wiederholen." })
        {
            Assert.Equal(1, Occurrences(text, "<li>" + message));
        }

        Assert.DoesNotContain("Zeichen lang", text, StringComparison.Ordinal);
        Assert.DoesNotContain("stimmen nicht überein", text, StringComparison.Ordinal);
    }

    [Fact] // AK-13
    public async Task Logout_ends_session_and_changes_security_stamp()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.LoginAsync(client, account.Email);
        var stampBefore = (await FindByEmailAsync(account.Email))!.SecurityStamp;
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);

        var (get, _) = await client.GetAsync("/Account/Logout");
        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);

        var (logout, _) = await client.PostAsync("/", "/Account/Logout", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", logout.Headers.Location?.OriginalString);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bg.auth=;", StringComparison.Ordinal));
        var (play, _) = await client.GetAsync("/play");
        Assert.Equal(HttpStatusCode.Redirect, play.StatusCode);
        Assert.Equal("/Account/Login", play.Headers.Location?.AbsolutePath);
        Assert.NotEqual(stampBefore, (await FindByEmailAsync(account.Email))!.SecurityStamp);
    }

    [Theory] // AK-1
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    public async Task Login_ignores_foreign_return_url(string returnUrl)
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        var (response, _) = await AccountTestFlows.LoginAsync(client, account.Email, returnUrl: returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/play", response.Headers.Location?.OriginalString);
    }

    [Fact] // AK-1
    public async Task Login_returns_to_requested_play_path()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        var (redirect, _) = await client.GetAsync("/play/irgendwas");
        Assert.Equal(HttpStatusCode.Redirect, redirect.StatusCode);
        var returnUrl = System.Web.HttpUtility.ParseQueryString(redirect.Headers.Location!.Query)["ReturnUrl"];

        var (response, _) = await AccountTestFlows.LoginAsync(client, account.Email, returnUrl: returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/play/irgendwas", response.Headers.Location?.OriginalString);
    }

    private async Task<Account?> FindByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
    }

    private static int Occurrences(string text, string part) =>
        text.Split(part, StringSplitOptions.None).Length - 1;

    private static void AssertGenericError(string html) =>
        Assert.Contains(Texts.ResourceManager.GetString("Login.GenericError", CultureInfo.GetCultureInfo("de"))!, WebUtility.HtmlDecode(html), StringComparison.Ordinal);

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => SessionCookieOnly.Any(name => c.StartsWith(name + "=", StringComparison.Ordinal)));

    /// <summary>Entfernt, was sich pro Anfrage ändert (Antiforgery-Token, Passkey-Optionen mit zufälliger Challenge, eingegebene E-Mail-Adresse).</summary>
    private static string Normalize(string html, string email) =>
        PasskeyOptionsPattern().Replace(TokenPattern().Replace(html, ""), "").Replace(email, "", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex("<input name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<script type=\"application/json\" data-passkey-options>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex PasskeyOptionsPattern();
}
