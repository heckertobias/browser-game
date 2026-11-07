using System.Globalization;
using System.Net;
using BrowserGame.Data;
using BrowserGame.Server.Features.Localization.Resources;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>002 AK-3: „Abmelden“ im Spielmenü öffnet per GET eine Bestätigungsseite; erst das Formular meldet ab.</summary>
public sealed class LogoutPageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    [Fact] // AK-3
    public async Task Get_shows_confirmation_and_keeps_session()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var stampBefore = await StampAsync(account.Email);

        var (get, html) = await client.GetAsync("/Account/Logout");

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        var form = Element(html, "logout-form", "form");
        Assert.Contains("method=\"post\"", form, StringComparison.Ordinal);
        Assert.Contains("action=\"/Account/Logout\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", form, StringComparison.Ordinal);
        Assert.Contains("type=\"submit\"", form, StringComparison.Ordinal);
        Assert.Contains("href=\"/play\"", Element(html, "back-to-game", "a"), StringComparison.Ordinal);
        Assert.False(ClearsSession(get));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/play")).Response.StatusCode);
        Assert.Equal(stampBefore, await StampAsync(account.Email));
    }

    [Fact] // AK-3
    public async Task Confirmation_form_signs_out()
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory);
        using var _c = client;

        var (logout, _) = await client.PostAsync("/Account/Logout", "/Account/Logout", new Dictionary<string, string>());

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", logout.Headers.Location?.OriginalString);
        Assert.Contains(logout.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bg.auth=;", StringComparison.Ordinal));
        var (play, _) = await client.GetAsync("/play");
        Assert.Equal(HttpStatusCode.Redirect, play.StatusCode);
        Assert.Equal("/Account/Login", play.Headers.Location?.AbsolutePath);
    }

    [Fact] // AK-3
    public async Task Get_redirects_home_when_signed_out()
    {
        using var client = factory.CreateFormClient();

        var (get, _) = await client.GetAsync("/Account/Logout");

        Assert.Equal(HttpStatusCode.Redirect, get.StatusCode);
        Assert.Equal("/", get.Headers.Location?.OriginalString);
    }

    [Theory] // AK-3
    [InlineData("de")]
    [InlineData("en")]
    public async Task Confirmation_follows_account_language(string language)
    {
        var (client, _) = await AccountTestFlows.SignInAsync(factory, language);
        using var _c = client;

        var (_, html) = await client.GetAsync("/Account/Logout");

        Assert.Contains($"<html lang=\"{language}\"", html, StringComparison.Ordinal);
        var culture = CultureInfo.GetCultureInfo(language);
        var text = HtmlTexts.Visible(html);
        foreach (var key in new[] { "Logout.Title", "Logout.Question", "Logout.BackToGame", "Index.SignOut" })
        {
            Assert.Contains(Texts.ResourceManager.GetString(key, culture)!, text, StringComparison.Ordinal);
        }

        var other = CultureInfo.GetCultureInfo(language == "de" ? "en" : "de");
        Assert.DoesNotContain(Texts.ResourceManager.GetString("Logout.Question", other)!, text, StringComparison.Ordinal);
    }

    private static string Element(string html, string testId, string tag)
    {
        var marker = html.IndexOf($"data-testid=\"{testId}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"{testId} fehlt");
        var start = html.LastIndexOf($"<{tag}", marker, StringComparison.Ordinal);
        var end = html.IndexOf($"</{tag}>", marker, StringComparison.Ordinal);
        return html[start..(end + tag.Length + 3)];
    }

    private async Task<string?> StampAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email))!.SecurityStamp;
    }

    private static bool ClearsSession(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
        && cookies.Any(c => c.StartsWith("bg.auth=", StringComparison.Ordinal));
}
