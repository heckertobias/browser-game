using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Localization;
using BrowserGame.Server.Pages;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;

namespace BrowserGame.Server.Tests;

/// <summary>006 T5: Umschalter im Layout und <c>POST /Language</c>.</summary>
public sealed partial class LanguageSwitchTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string CultureCookie = ".AspNetCore.Culture";

    private FormClient ClientWith(string acceptLanguage)
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return client;
    }

    private static Task<(HttpResponseMessage Response, string Html)> SwitchAsync(
        FormClient client, string pageUrl, string? language, string? returnUrl)
    {
        var fields = new Dictionary<string, string>();
        if (language is not null)
        {
            fields["Language"] = language;
        }

        if (returnUrl is not null)
        {
            fields["ReturnUrl"] = returnUrl;
        }

        return client.PostAsync(pageUrl, "/Language", fields);
    }

    private static string? CultureCookieValue(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(v => v.Split(';')[0]).Where(v => v.StartsWith(CultureCookie + "=", StringComparison.Ordinal))
                .Select(v => WebUtility.UrlDecode(v[(CultureCookie.Length + 1)..])).SingleOrDefault()
            : null;

    private static string Form(string html) =>
        FormPattern().Match(html) is { Success: true } match ? match.Value : throw new InvalidOperationException("Kein Umschalter im HTML.");

    private static string ReturnUrlOf(string html) =>
        WebUtility.HtmlDecode(ReturnUrlPattern().Match(Form(html)).Groups[1].Value);

    private async Task<string> SecurityStampAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        return (await users.FindByEmailAsync(email))!.SecurityStamp!;
    }

    private async Task<string> StoredLanguageAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        return (await users.FindByEmailAsync(email))!.Language;
    }

    [Theory] // AK-3
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Login?ReturnUrl=%2Fplay")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/CompleteRegistration")]
    public async Task Switcher_is_on_every_player_page(string url)
    {
        using var client = ClientWith("de");

        var (_, html) = await client.GetAsync(url);

        var form = Form(html);
        Assert.Contains("action=\"/Language\"", form, StringComparison.Ordinal);
        Assert.Contains("data-language-switch", form, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", form, StringComparison.Ordinal);
        Assert.Equal(url, ReturnUrlOf(html));
        Assert.Contains("name=\"Language\" value=\"de\" lang=\"de\"", form, StringComparison.Ordinal);
        Assert.Contains("name=\"Language\" value=\"en\" lang=\"en\"", form, StringComparison.Ordinal);
        Assert.Contains(">Deutsch</button>", form, StringComparison.Ordinal);
        Assert.Contains(">English</button>", form, StringComparison.Ordinal);
    }

    [Theory] // AK-3
    [InlineData("de", "en", "Sprache")]
    [InlineData("en", "de", "Language")]
    public async Task Active_language_is_marked_and_disabled(string active, string other, string label)
    {
        using var client = ClientWith(active);

        var (_, html) = await client.GetAsync("/");

        var form = Form(html);
        Assert.Contains($"aria-label=\"{label}\"", form, StringComparison.Ordinal);
        Assert.Matches($"<button[^>]*value=\"{active}\"[^>]*disabled[^>]*aria-current=\"true\"", form);
        Assert.DoesNotMatch($"<button[^>]*value=\"{other}\"[^>]*(disabled|aria-current)", form);
    }

    [Fact] // AK-3
    public async Task Switching_sets_cookie_and_returns_to_the_page()
    {
        using var client = ClientWith("de");

        var (response, _) = await SwitchAsync(client, "/Account/Register", "en", "/Account/Register");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Account/Register", response.Headers.Location?.OriginalString);
        Assert.Equal("c=en|uic=en", CultureCookieValue(response));
        var (_, html) = await client.GetAsync("/Account/Register");
        Assert.Contains("<html lang=\"en\"", html, StringComparison.Ordinal);
        Assert.Contains("Sign up", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-3
    public async Task Return_url_keeps_the_query()
    {
        using var client = ClientWith("de");

        var (response, _) = await SwitchAsync(client, "/", "en", "/Account/Login?ReturnUrl=%2Fplay");

        Assert.Equal("/Account/Login?ReturnUrl=%2Fplay", response.Headers.Location?.OriginalString);
        var (_, html) = await client.GetAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("Sign in to play.", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Theory] // AK-3
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\t/evil.example")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Foreign_or_missing_return_url_leads_home(string? returnUrl)
    {
        using var client = ClientWith("de");

        var (response, _) = await SwitchAsync(client, "/", "en", returnUrl);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Theory] // AK-3
    [InlineData("fr")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Unknown_language_falls_back_to_english(string? language)
    {
        using var client = ClientWith("de");

        var (response, _) = await SwitchAsync(client, "/", language, "/");

        Assert.Equal("c=en|uic=en", CultureCookieValue(response));
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        using var client = ClientWith("de");

        var (response, _) = await client.PostWithTokenAsync("/Language", new Dictionary<string, string> { ["Language"] = "en" }, "ungueltig");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(CultureCookieValue(response));
    }

    [Fact]
    public async Task Get_redirects_home()
    {
        using var client = ClientWith("de");

        var (response, _) = await client.GetAsync("/Language");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    [Fact] // AK-3
    public async Task Switch_on_a_result_page_returns_to_the_get_page()
    {
        using var client = ClientWith("de");
        var (_, sent) = await client.PostAsync("/Account/Register", "/Account/Register",
            new Dictionary<string, string> { ["Email"] = AccountTestFlows.UniqueEmail() });
        var returnUrl = ReturnUrlOf(sent);

        var (response, _) = await SwitchAsync(client, "/Account/Register", "en", returnUrl);

        Assert.Equal("/Account/Register", response.Headers.Location?.OriginalString);
        var (_, html) = await client.GetAsync(response.Headers.Location!.OriginalString);
        Assert.Contains("Enter your email address. We'll send you a link to create your account.", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-5
    public async Task Signed_in_switch_saves_account_language_and_keeps_the_session()
    {
        using var client = ClientWith("de");
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.LoginAsync(client, account.Email);
        var stamp = await SecurityStampAsync(account.Email);

        await SwitchAsync(client, "/", "en", "/");

        Assert.Equal("en", await StoredLanguageAsync(account.Email));
        Assert.Equal(stamp, await SecurityStampAsync(account.Email));

        await SwitchAsync(client, "/", "de", "/");

        Assert.Equal("de", await StoredLanguageAsync(account.Email));
    }

    [Fact] // AK-5
    public async Task Anonymous_switch_changes_no_account()
    {
        using var client = ClientWith("de");
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        await SwitchAsync(client, "/", "en", "/");

        Assert.Equal("de", await StoredLanguageAsync(account.Email));
    }

    [Fact]
    public void Switching_is_not_rate_limited()
    {
        Assert.Null(typeof(LanguageModel).GetCustomAttribute<EnableRateLimitingAttribute>());
    }

    [Theory] // AK-3
    [InlineData("")]
    [InlineData("de")]
    public void Language_names_are_endonyms_in_every_language(string culture)
    {
        var expected = new Dictionary<string, string> { ["de"] = "Deutsch", ["en"] = "English" };

        foreach (var code in GameLanguages.Supported)
        {
            Assert.Equal(expected[code], Texts.ResourceManager.GetString($"Language.{code}", CultureInfo.GetCultureInfo(culture)));
        }
    }

    [Theory] // AK-3
    [InlineData("de")]
    [InlineData("en")]
    public async Task Switch_asks_before_losing_entries_in_the_current_language(string language)
    {
        using var client = ClientWith(language);

        var (_, html) = await client.GetAsync("/");

        var attribute = ConfirmPattern().Match(Form(html));
        Assert.True(attribute.Success);
        var message = WebUtility.HtmlDecode(attribute.Groups[1].Value);
        var expected = Texts.ResourceManager.GetString("Language.Confirm", CultureInfo.GetCultureInfo(language));
        Assert.False(string.IsNullOrWhiteSpace(expected));
        Assert.NotEqual("Language.Confirm", expected);
        Assert.Equal(expected, message);
        if (language == "en")
        {
            Assert.DoesNotMatch("[äöüÄÖÜß]", message);
        }
        else
        {
            Assert.NotEqual(Texts.ResourceManager.GetString("Language.Confirm", CultureInfo.GetCultureInfo("en")), message);
        }
    }

    [Theory] // AK-3
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Login?ReturnUrl=%2Fplay")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/CompleteRegistration")]
    public async Task Layout_loads_language_js(string url)
    {
        using var client = ClientWith("de");

        var (_, html) = await client.GetAsync(url);

        Assert.Single(ScriptPattern().Matches(html));
    }

    [GeneratedRegex("<form[^>]*data-language-switch.*?</form>", RegexOptions.Singleline)]
    private static partial Regex FormPattern();

    [GeneratedRegex("data-confirm=\"([^\"]*)\"")]
    private static partial Regex ConfirmPattern();

    [GeneratedRegex("<script type=\"module\" src=\"/js/language\\.js(\\?v=[^\"]+)?\"></script>")]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("name=\"ReturnUrl\" value=\"([^\"]*)\"")]
    private static partial Regex ReturnUrlPattern();
}
