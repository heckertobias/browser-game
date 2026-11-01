using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Localization.Resources;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

public sealed partial class AccountPagesTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly string[] SessionCookieOnly = ["bg.auth"];

    [Fact] // AK-9
    public async Task Failed_logins_look_identical()
    {
        using var client = factory.CreateFormClient();
        var confirmed = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var pendingEmail = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RequestRegistrationAsync(client, pendingEmail);
        var unknown = AccountTestFlows.UniqueEmail();

        var wrongPassword = await AccountTestFlows.LoginAsync(client, confirmed.Email, "falsches-passwort");
        var unknownEmail = await AccountTestFlows.LoginAsync(client, unknown);
        var pending = await AccountTestFlows.LoginAsync(client, pendingEmail);

        foreach (var (response, html) in new[] { wrongPassword, unknownEmail, pending })
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertGenericError(html);
            Assert.False(HasSessionCookie(response));
        }

        var expected = Normalize(wrongPassword.Html, confirmed.Email);
        Assert.Equal(expected, Normalize(unknownEmail.Html, unknown));
        Assert.Equal(expected, Normalize(pending.Html, pendingEmail));
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
