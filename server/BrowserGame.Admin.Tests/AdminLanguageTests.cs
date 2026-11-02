using System.Globalization;
using System.Net;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;

namespace BrowserGame.Admin.Tests;

/// <summary>005 AK-14: Das Admin ist fest Englisch – Browser-Sprache und Culture-Cookie des Spiels ändern nichts.</summary>
public sealed class AdminLanguageTests
{
    private const string GermanCookie = ".AspNetCore.Culture=c=de|uic=de";

    public static TheoryData<string?, string?> Requests => new()
    {
        { null, null },
        { "de", null },
        { null, GermanCookie },
        { "de-DE,de;q=0.9", GermanCookie },
    };

    private static FormClient Client(AdminFactory factory, string? acceptLanguage, string? cookie)
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

    private static string IdentityText(string key, CultureInfo culture, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, LocalizedIdentityErrorDescriber.ResourceManager.GetString(key, culture)!, args);

    [Theory] // 005 AK-14
    [MemberData(nameof(Requests))]
    public async Task Identity_errors_are_english_regardless_of_browser_language(string? acceptLanguage, string? cookie)
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        using var client = Client(factory, acceptLanguage, cookie);

        var (_, chooseMethod) = await client.PostAsync($"/Invite?token={token}", "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Tobias", ["Email"] = "admin@example.org" });
        var (_, html) = await client.PostWithTokenAsync("/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = "kurz", ["ConfirmPassword"] = "kurz" },
            FormClient.AntiforgeryToken(chooseMethod));
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains(IdentityText("PasswordTooShort", CultureInfo.InvariantCulture, 12), text, StringComparison.Ordinal);
        Assert.DoesNotContain(IdentityText("PasswordTooShort", CultureInfo.GetCultureInfo("de"), 12), text, StringComparison.Ordinal);
    }

    [Theory] // 005 AK-14
    [MemberData(nameof(Requests))]
    public async Task Validation_errors_are_english_regardless_of_browser_language(string? acceptLanguage, string? cookie)
    {
        await using var factory = new AdminFactory();
        using var client = Client(factory, acceptLanguage, cookie);

        var (_, html) = await AdminTestFlows.LoginPasswordAsync(client, "", "");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains(AdminTexts.ResourceManager.GetString("Validation.EmailRequired", CultureInfo.InvariantCulture)!, text, StringComparison.Ordinal);
        Assert.Contains(AdminTexts.ResourceManager.GetString("Validation.PasswordRequired", CultureInfo.InvariantCulture)!, text, StringComparison.Ordinal);
    }
}
