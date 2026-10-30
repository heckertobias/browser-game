using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Localization.Resources;
using BrowserGame.Server.Features.Accounts.Presentation.Account;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace BrowserGame.Server.Tests;

/// <summary>003 T14: Passwort-Anmeldung – Sperre und Rate-Limit (AK-10), 30 Tage angemeldet (AK-11).</summary>
public sealed partial class LoginProtectionTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static string GenericError => Texts.ResourceManager.GetString("Login.GenericError", CultureInfo.GetCultureInfo("de"))!;

    [Fact] // AK-10, AK-9
    public async Task Five_failed_logins_lock_the_account_for_five_minutes()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var other = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        for (var i = 0; i < 4; i++)
        {
            await AccountTestFlows.LoginAsync(client, account.Email, "falsches-passwort");
        }

        // Vor dem 5. Fehlversuch keine Sperre.
        Assert.Null((await FindByEmailAsync(account.Email))!.LockoutEnd);

        var (_, wrongHtml) = await AccountTestFlows.LoginAsync(client, account.Email, "falsches-passwort");
        var lockoutEnd = (await FindByEmailAsync(account.Email))!.LockoutEnd;
        Assert.NotNull(lockoutEnd);
        Assert.InRange(lockoutEnd.Value - DateTimeOffset.UtcNow, TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5));

        // Auch das richtige Passwort scheitert, und die Antwort verrät die Sperre nicht (AK-9).
        var (locked, lockedHtml) = await AccountTestFlows.LoginAsync(client, account.Email);
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        Assert.False(HasSessionCookie(locked));
        Assert.Contains(GenericError, WebUtility.HtmlDecode(lockedHtml), StringComparison.Ordinal);
        Assert.Equal(Normalize(wrongHtml), Normalize(lockedHtml));

        // Gesperrt ist der Account, nicht der Client.
        var (otherLogin, _) = await AccountTestFlows.LoginAsync(client, other.Email);
        Assert.Equal(HttpStatusCode.Redirect, otherLogin.StatusCode);

        var lockout = factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout;
        Assert.Equal(5, lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(5), lockout.DefaultLockoutTimeSpan);
    }

    [Fact] // AK-10
    public void Login_and_registration_are_rate_limited()
    {
        foreach (var model in new[] { typeof(LoginModel), typeof(RegisterModel), typeof(CompleteRegistrationModel) })
        {
            Assert.Equal(WebDefaults.AuthRateLimitPolicy, model.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName);
        }

        Assert.Equal(10, WebDefaults.AuthRequestsPerMinute);
    }

    [Fact] // AK-10
    public async Task Too_many_login_posts_are_answered_with_429()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthRequestsPerMinute", "3"));
        using var client = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        var (_, page) = await client.GetAsync("/Account/Login");
        var token = FormClient.AntiforgeryToken(page);
        var fields = new Dictionary<string, string>
        {
            ["Email"] = AccountTestFlows.UniqueEmail(),
            ["Password"] = "falsches-passwort",
            ["ReturnUrl"] = "",
        };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostWithTokenAsync("/Account/Login", fields, token)).Response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/Account/Login")).Response.StatusCode);
    }

    [Fact] // AK-11
    public async Task Password_login_keeps_the_session_for_30_days()
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var (response, _) = await AccountTestFlows.LoginAsync(client, account.Email);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));
        var expires = DateTimeOffset.Parse(
            cookie.Split(';').Select(p => p.Trim()).Single(p => p.StartsWith("expires=", StringComparison.OrdinalIgnoreCase))["expires=".Length..],
            CultureInfo.InvariantCulture);
        Assert.InRange((expires - DateTimeOffset.UtcNow).TotalDays, 29, 31);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);
        Assert.Equal(TimeSpan.FromDays(30), options.ExpireTimeSpan);
        Assert.False(options.SlidingExpiration);
    }

    private async Task<Account?> FindByEmailAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
    }

    private static bool HasSessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies) && cookies.Any(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));

    // Antiforgery-Token und Passkey-Optionen sind pro Anfrage zufällig.
    private static string Normalize(string html) => PasskeyOptionsPattern().Replace(TokenPattern().Replace(html, ""), "");

    [GeneratedRegex("<input name=\"__RequestVerificationToken\"[^>]*>")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<script type=\"application/json\" data-passkey-options>.*?</script>", RegexOptions.Singleline)]
    private static partial Regex PasskeyOptionsPattern();
}
