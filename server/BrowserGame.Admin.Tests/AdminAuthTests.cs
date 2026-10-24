using System.Net;
using BrowserGame.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BrowserGame.Admin.Tests;

/// <summary>Jede Testmethode bekommt eine frische Datenbank (eigene Factory), weil der erste Admin global ist.</summary>
public sealed class AdminAuthTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact] // AK-1, AK-4b, AK-4c
    public async Task Setup_link_from_log_creates_first_admin_with_password_totp_and_recovery_codes()
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);

        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, token, "Tobias", "admin@example.org");

        Assert.Equal(10, admin.RecoveryCodes.Count);
        var (home, html) = await admin.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Contains("Übersicht", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-1: Link ist einmalig
    public async Task Setup_link_cannot_be_used_twice()
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        await AdminTestFlows.RedeemWithPasswordAsync(factory, token, "Tobias", "admin@example.org");

        using var other = factory.CreateFormClient();
        var (_, html) = await other.GetAsync($"/Invite?token={token}");

        Assert.Contains("ungültig, abgelaufen oder wurde schon benutzt", html, StringComparison.Ordinal);
    }

    [Fact] // AK-1: Ein neuer Start ohne eingerichteten Admin erzeugt einen neuen Link, der alte wird ungültig
    public async Task Restart_without_admin_replaces_the_setup_link()
    {
        await using var factory = new AdminFactory();
        var first = AdminTestFlows.SetupTokenFromLog(factory);

        await using var restarted = factory.WithWebHostBuilder(_ => { });
        _ = restarted.Server;
        var second = AdminTestFlows.SetupTokenFromLog(factory);

        Assert.NotEqual(first, second);
        using var client = factory.CreateFormClient();
        Assert.Contains("ungültig", (await client.GetAsync($"/Invite?token={first}")).Html, StringComparison.Ordinal);
    }

    [Fact] // AK-3
    public async Task Anonymous_requests_are_redirected_to_login()
    {
        await using var factory = new AdminFactory();
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact] // AK-4b: Passwort allein genügt nicht, danach ist der Code Pflicht
    public async Task Password_login_requires_authenticator_code()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        using var client = factory.CreateFormClient();

        var (afterPassword, _) = await AdminTestFlows.LoginPasswordAsync(client, admin.Email);
        Assert.Equal("/LoginTwoFactor", afterPassword.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/")).Response.StatusCode); // noch nicht angemeldet

        var (wrong, wrongHtml) = await AdminTestFlows.LoginCodeAsync(client, "000000");
        Assert.Equal(HttpStatusCode.OK, wrong.StatusCode);
        Assert.Contains("Der Code stimmt nicht", wrongHtml, StringComparison.Ordinal);

        var (right, _) = await AdminTestFlows.LoginCodeAsync(client, Totp.Code(admin.TotpKey));
        Assert.Equal("/", right.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).Response.StatusCode);
    }

    [Fact] // AK-4c: Wiederherstellungscode funktioniert genau einmal
    public async Task Recovery_code_works_once()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        var code = admin.RecoveryCodes[0];

        using var first = factory.CreateFormClient();
        await AdminTestFlows.LoginPasswordAsync(first, admin.Email);
        Assert.Equal("/", (await AdminTestFlows.LoginCodeAsync(first, code, recovery: true)).Response.Headers.Location?.OriginalString);

        using var second = factory.CreateFormClient();
        await AdminTestFlows.LoginPasswordAsync(second, admin.Email);
        var (again, html) = await AdminTestFlows.LoginCodeAsync(second, code, recovery: true);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Contains("Der Code stimmt nicht", html, StringComparison.Ordinal);
    }

    [Fact] // AK-2: Spieler-Accounts gelten im Admin nicht
    public async Task Player_account_cannot_log_into_admin()
    {
        await using var factory = new AdminFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var hasher = new PasswordHasher<Account>();
            var player = new Account { UserName = "Spieler", NormalizedUserName = "SPIELER", Email = "spieler@example.org", NormalizedEmail = "SPIELER@EXAMPLE.ORG", EmailConfirmed = true };
            player.PasswordHash = hasher.HashPassword(player, AdminTestFlows.Password);
            var game = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            game.Users.Add(player);
            await game.SaveChangesAsync(Ct);
        }

        using var client = factory.CreateFormClient();
        var (response, html) = await AdminTestFlows.LoginPasswordAsync(client, "spieler@example.org");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Anmeldung fehlgeschlagen", html, StringComparison.Ordinal);
    }

    [Fact] // AK-4d: höchstens 8 Stunden, nicht verlängernd
    public void Admin_session_lasts_at_most_eight_hours()
    {
        using var factory = new AdminFactory();
        var cookie = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(IdentityConstants.ApplicationScheme);

        Assert.Equal(TimeSpan.FromHours(8), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
        Assert.Equal(Microsoft.AspNetCore.Http.SameSiteMode.Strict, cookie.Cookie.SameSite);
        Assert.Equal("bg.admin.application", cookie.Cookie.Name);
    }

    [Fact] // AK-4: nach 5 Fehlversuchen gesperrt – auch das richtige Passwort hilft dann nicht
    public async Task Account_is_locked_after_five_wrong_passwords()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        using var client = factory.CreateFormClient();

        for (var i = 0; i < 5; i++)
        {
            await AdminTestFlows.LoginPasswordAsync(client, admin.Email, "falsches-passwort");
        }

        var (response, _) = await AdminTestFlows.LoginPasswordAsync(client, admin.Email);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); // kein Redirect zum 2FA-Schritt
    }

    [Fact] // AK-4: zu viele Absendungen pro Minute werden gebremst
    public async Task Too_many_login_posts_are_rate_limited()
    {
        await using var factory = new AdminFactory { AuthRequestsPerMinute = 3 };
        using var client = factory.CreateFormClient();
        var (_, page) = await client.GetAsync("/Login");
        var token = FormClient.AntiforgeryToken(page);
        var fields = new Dictionary<string, string> { ["Email"] = "x@example.org", ["Password"] = "falsch" };

        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 4; i++)
        {
            statuses.Add((await client.PostWithTokenAsync("/Login?handler=Password", fields, token)).Response.StatusCode);
        }

        Assert.Equal([HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.TooManyRequests], statuses);
    }
}
