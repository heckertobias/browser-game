using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Net.Http.Headers;

namespace BrowserGame.Server.Tests;

public sealed partial class LanguageSelectionTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly string[] German =
        ["Bitte E-Mail angeben.", "Bitte einen Spielernamen angeben.", "Bitte Passwort angeben.", "Bitte das Passwort wiederholen."];

    private static readonly string[] English =
        ["Please enter your email address.", "Please enter a player name.", "Please enter your password.", "Please repeat the password."];

    private FormClient Client(string? acceptLanguage, string? cookie = null)
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        if (acceptLanguage is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (cookie is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        return client;
    }

    private async Task<string> LangAsync(string? acceptLanguage, string? cookie = null, string url = "/")
    {
        using var client = Client(acceptLanguage, cookie);
        var (_, html) = await client.GetAsync(url);
        return LangPattern().Match(html).Groups[1].Value;
    }

    private async Task<string> EmptyRegistrationAsync(string? acceptLanguage)
    {
        using var client = Client(acceptLanguage);
        var (_, html) = await client.PostAsync("/Account/Register", "/Account/Register", new Dictionary<string, string>());
        return WebUtility.HtmlDecode(html);
    }

    private static void AssertMessages(string html, string[] expected, string[] absent)
    {
        foreach (var message in expected)
        {
            Assert.Equal(1, Regex.Count(html, $"<li>{Regex.Escape(message)}</li>"));
        }

        foreach (var message in absent)
        {
            Assert.DoesNotContain(message, html, StringComparison.Ordinal);
        }
    }

    [Theory] // AK-1
    [InlineData("de")]
    [InlineData("de-DE")]
    [InlineData("de-AT")]
    [InlineData("de-CH")]
    [InlineData("de-AT,en;q=0.8")]
    [InlineData("DE-at")]
    public async Task German_browser_gets_german(string acceptLanguage) =>
        Assert.Equal("de", await LangAsync(acceptLanguage));

    [Fact] // AK-1
    public async Task German_browser_gets_german_validation_messages() =>
        AssertMessages(await EmptyRegistrationAsync("de-AT"), German, English);

    [Theory] // AK-2
    [InlineData("en")]
    [InlineData("en-US")]
    [InlineData("fr")]
    [InlineData("fr, de")]
    [InlineData("fr-FR,de;q=0.9")]
    [InlineData("*")]
    [InlineData("xx")]
    [InlineData("zh-Hant-TW")]
    [InlineData(null)]
    public async Task Everyone_else_gets_english(string? acceptLanguage) =>
        Assert.Equal("en", await LangAsync(acceptLanguage));

    [Theory] // AK-2
    [InlineData("fr, de")]
    [InlineData(null)]
    public async Task Everyone_else_gets_english_validation_messages(string? acceptLanguage) =>
        AssertMessages(await EmptyRegistrationAsync(acceptLanguage), English, German);

    [Fact] // AK-2
    public async Task Query_string_cannot_switch_language() =>
        Assert.Equal("en", await LangAsync("en", url: "/?culture=de&ui-culture=de"));

    [Theory] // AK-4
    [InlineData("c=en|uic=en", "de", "en")]
    [InlineData("c=de|uic=de", "en", "de")]
    [InlineData("c=fr|uic=fr", "de", "de")]
    [InlineData("Unsinn", "de", "de")]
    [InlineData("c=de-AT|uic=de-AT", null, "de")]
    [InlineData("uic=de-AT", "en", "en")]
    [InlineData("c=de-CH", "en", "en")]
    [InlineData("c=fr-FR|uic=fr-FR", "de-AT", "de")]
    [InlineData("c=de-AT|uic=de-AT", "en", "de")]
    public async Task Cookie_wins_over_accept_language(string cookieValue, string? acceptLanguage, string expected) =>
        Assert.Equal(expected, await LangAsync(acceptLanguage, $"{CookieRequestCultureProvider.DefaultCookieName}={Uri.EscapeDataString(cookieValue)}"));

    [Fact] // AK-4
    public async Task Cookie_written_by_the_server_is_read_back()
    {
        var context = new DefaultHttpContext { RequestServices = factory.Services };
        LanguageCookie.Append(context.Response, "en");
        var cookie = SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single());

        Assert.Equal("en", await LangAsync("de", $"{cookie.Name}={cookie.Value}"));
    }

    [Theory] // AK-4
    [InlineData("en")]
    [InlineData("fr")]
    public async Task Static_assets_are_not_affected_by_language(string language)
    {
        using var client = Client("de", $"{CookieRequestCultureProvider.DefaultCookieName}={Uri.EscapeDataString($"c={language}|uic={language}")}");
        var (response, _) = await client.GetAsync("/play/assets/test-asset.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Content.Headers.Contains("Content-Language"));
    }

    [GeneratedRegex("<html lang=\"([^\"]*)\"")]
    private static partial Regex LangPattern();
}
