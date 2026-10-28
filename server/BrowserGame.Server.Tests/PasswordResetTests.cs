using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using BrowserGame.Server.Pages.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Options;

namespace BrowserGame.Server.Tests;

public sealed partial class PasswordResetTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string NewPassword = "ein-ganz-neues-passwort";
    private const string InvalidText = "ungültig, abgelaufen oder wurde schon benutzt";

    private async Task<(AccountTestFlows.CreatedAccount Account, string Link)> RequestLinkAsync(FormClient client)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.ForgotPasswordAsync(client, account.Email);
        var mail = Assert.Single(factory.Mails.To(account.Email));
        return (account, AccountTestFlows.LinkFromMail(mail.Text));
    }

    private async Task<Account?> FindByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static string Normalize(string html, string email) =>
        TokenPattern().Replace(html, "").Replace(email, "", StringComparison.OrdinalIgnoreCase);

    [Fact] // AK-14
    public async Task Login_links_to_forgot_password()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await client.GetAsync("/Account/Login");

        Assert.Contains("href=\"/Account/ForgotPassword\"", html, StringComparison.Ordinal);
        Assert.Contains("Passwort vergessen?", Decode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-14
    public async Task Forgot_password_answers_the_same_for_known_and_unknown_address()
    {
        using var client = factory.CreateFormClient();
        var known = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var unknown = AccountTestFlows.UniqueEmail();

        var (knownResponse, knownHtml) = await AccountTestFlows.ForgotPasswordAsync(client, known.Email);
        var (unknownResponse, unknownHtml) = await AccountTestFlows.ForgotPasswordAsync(client, unknown);

        Assert.Equal(HttpStatusCode.OK, knownResponse.StatusCode);
        Assert.Equal(unknownResponse.StatusCode, knownResponse.StatusCode);
        Assert.Contains("Falls es zu dieser Adresse einen bestätigten Account gibt", Decode(knownHtml), StringComparison.Ordinal);
        Assert.Equal(Normalize(unknownHtml, unknown), Normalize(knownHtml, known.Email));
        var mail = Assert.Single(factory.Mails.To(known.Email));
        Assert.Equal("Passwort zurücksetzen", mail.Subject);
        Assert.Matches(@"/Account/ResetPassword\?userId=[^&\s]+&code=\S+", AccountTestFlows.LinkFromMail(mail.Text));
        Assert.Empty(factory.Mails.To(unknown));
    }

    [Fact] // AK-14
    public async Task Forgot_password_sends_nothing_to_unconfirmed_account()
    {
        using var client = factory.CreateFormClient();
        var email = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());
        var unknown = AccountTestFlows.UniqueEmail();

        var (_, html) = await AccountTestFlows.ForgotPasswordAsync(client, email);
        var (_, unknownHtml) = await AccountTestFlows.ForgotPasswordAsync(client, unknown);

        Assert.Equal(Normalize(unknownHtml, unknown), Normalize(html, email));
        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Equal("Bitte bestätige deine E-Mail-Adresse", mail.Subject);
    }

    [Fact] // AK-14
    public async Task Empty_forgot_password_form_shows_required_message()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await AccountTestFlows.ForgotPasswordAsync(client, "");

        Assert.Contains("Bitte E-Mail angeben.", Decode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("Falls es zu dieser Adresse", Decode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-15
    public async Task Reset_link_sets_new_password_and_works_only_once()
    {
        using var client = factory.CreateFormClient();
        var (account, link) = await RequestLinkAsync(client);

        var (_, form) = await client.GetAsync(link);
        Assert.Contains("name=\"Password\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"ConfirmPassword\"", form, StringComparison.Ordinal);

        var (reset, done) = await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword);
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Contains("Dein Passwort wurde geändert", Decode(done), StringComparison.Ordinal);

        var (_, again) = await AccountTestFlows.ResetPasswordAsync(client, link, "noch-ein-anderes-passwort");
        Assert.Contains(InvalidText, Decode(again), StringComparison.Ordinal);
        var (_, getAgain) = await client.GetAsync(link);
        Assert.Contains(InvalidText, Decode(getAgain), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Password\"", getAgain, StringComparison.Ordinal);

        var (newLogin, _) = await AccountTestFlows.LoginAsync(client, account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, newLogin.StatusCode);
        Assert.Equal("/play", newLogin.Headers.Location?.OriginalString);
        using var other = factory.CreateFormClient();
        var (oldLogin, oldHtml) = await AccountTestFlows.LoginAsync(other, account.Email);
        Assert.Equal(HttpStatusCode.OK, oldLogin.StatusCode);
        Assert.Contains("Anmeldung fehlgeschlagen", Decode(oldHtml), StringComparison.Ordinal);
        var (otherNew, _) = await AccountTestFlows.LoginAsync(other, account.Email, "noch-ein-anderes-passwort");
        Assert.Equal(HttpStatusCode.OK, otherNew.StatusCode);
    }

    [Fact] // AK-15
    public async Task Reset_link_expires()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.Zero)));
        using var client = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        var (account, link) = await RequestLinkAsync(client);

        var (_, get) = await client.GetAsync(link);
        var (_, post) = await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword);

        Assert.Contains(InvalidText, Decode(get), StringComparison.Ordinal);
        Assert.Contains(InvalidText, Decode(post), StringComparison.Ordinal);
        var (login, _) = await AccountTestFlows.LoginAsync(client, account.Email);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    [Fact] // AK-15
    public void Token_lifetime_is_three_hours_and_sessions_are_checked_every_minute()
    {
        var services = factory.Services;

        Assert.Equal(TimeSpan.FromHours(3), services.GetRequiredService<IOptions<DataProtectionTokenProviderOptions>>().Value.TokenLifespan);
        Assert.Equal(TimeSpan.FromMinutes(1), services.GetRequiredService<IOptions<SecurityStampValidatorOptions>>().Value.ValidationInterval);
    }

    [Fact] // AK-15
    public async Task Reset_ends_all_other_sessions()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        using var sessionClient = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        using var resetClient = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.LoginAsync(sessionClient, account.Email);
        Assert.Equal(HttpStatusCode.OK, (await sessionClient.GetAsync("/play")).Response.StatusCode);
        var stampBefore = (await FindByEmailAsync(account.Email))!.SecurityStamp;

        await AccountTestFlows.ForgotPasswordAsync(resetClient, account.Email);
        var link = AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(account.Email)).Text);
        await AccountTestFlows.ResetPasswordAsync(resetClient, link, NewPassword);

        var (after, _) = await sessionClient.GetAsync("/play");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Contains("/Account/Login", after.Headers.Location?.OriginalString, StringComparison.Ordinal);
        Assert.NotEqual(stampBefore, (await FindByEmailAsync(account.Email))!.SecurityStamp);
    }

    [Fact] // AK-15
    public async Task Reset_lifts_lockout()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        for (var i = 0; i < 5; i++)
        {
            await AccountTestFlows.LoginAsync(client, account.Email, "falsches-passwort");
        }

        var (locked, lockedHtml) = await AccountTestFlows.LoginAsync(client, account.Email);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.Contains("Anmeldung fehlgeschlagen", Decode(lockedHtml), StringComparison.Ordinal);

        await AccountTestFlows.ForgotPasswordAsync(client, account.Email);
        var link = AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(account.Email)).Text);
        await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword);

        var (login, _) = await AccountTestFlows.LoginAsync(client, account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal("/play", login.Headers.Location?.OriginalString);
    }

    [Theory] // AK-15
    [InlineData("none")]
    [InlineData("garbage")]
    [InlineData("garbage-code")]
    [InlineData("wrong-purpose")]
    public async Task Invalid_reset_links_are_rejected(string kind)
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var user = (await FindByEmailAsync(account.Email))!;
        string confirmationToken;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            confirmationToken = AccountLinks.EncodeToken(await users.GenerateEmailConfirmationTokenAsync(user));
        }

        var link = kind switch
        {
            "none" => "/Account/ResetPassword",
            "garbage" => "/Account/ResetPassword?userId=x&code=kaputt",
            "garbage-code" => $"/Account/ResetPassword?userId={user.Id}&code=kaputt",
            _ => $"/Account/ResetPassword?userId={user.Id}&code={confirmationToken}",
        };

        var (_, html) = await client.GetAsync(link);

        Assert.Contains(InvalidText, Decode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("name=\"Password\"", html, StringComparison.Ordinal);
    }

    [Fact] // AK-15
    public async Task Short_or_mismatched_new_password_is_rejected_and_link_stays_valid()
    {
        using var client = factory.CreateFormClient();
        var (account, link) = await RequestLinkAsync(client);

        var (_, tooShort) = await AccountTestFlows.ResetPasswordAsync(client, link, "kurz");
        var (_, mismatch) = await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword, "etwas-ganz-anderes");

        Assert.Contains("name=\"Password\"", tooShort, StringComparison.Ordinal);
        Assert.Contains("mindestens 10 Zeichen", Decode(tooShort), StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", mismatch, StringComparison.Ordinal);
        Assert.Contains("stimmen nicht überein", Decode(mismatch), StringComparison.Ordinal);
        var (_, done) = await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword);
        Assert.Contains("Dein Passwort wurde geändert", Decode(done), StringComparison.Ordinal);
        var (login, _) = await AccountTestFlows.LoginAsync(client, account.Email, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
    }

    [Fact] // AK-15
    public async Task Empty_reset_form_shows_one_message_per_field()
    {
        using var client = factory.CreateFormClient();
        var (_, link) = await RequestLinkAsync(client);

        var (_, html) = await AccountTestFlows.ResetPasswordAsync(client, link, "", "");

        var text = Decode(html);
        Assert.Equal(1, Occurrences(text, "<li>Bitte ein neues Passwort angeben."));
        Assert.Equal(1, Occurrences(text, "<li>Bitte das Passwort wiederholen."));
    }

    [Fact] // AK-14, AK-15
    public async Task Forgot_and_reset_pages_are_rate_limited()
    {
        foreach (var model in new[] { typeof(ForgotPasswordModel), typeof(ResetPasswordModel) })
        {
            var attribute = model.GetCustomAttribute<EnableRateLimitingAttribute>();
            Assert.Equal(WebDefaults.AuthRateLimitPolicy, attribute?.PolicyName);
        }

        await using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthRequestsPerMinute", "3"));
        using var client = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        var (_, page) = await client.GetAsync("/Account/ForgotPassword");
        var token = FormClient.AntiforgeryToken(page);
        var fields = new Dictionary<string, string> { ["Email"] = "x@example.org" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostWithTokenAsync("/Account/ForgotPassword", fields, token)).Response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }

    private static int Occurrences(string text, string part) =>
        text.Split(part, StringSplitOptions.None).Length - 1;

    [GeneratedRegex("<input name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenPattern();
}
