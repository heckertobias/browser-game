using System.Net;

namespace BrowserGame.Admin.Tests;

/// <summary>006: Der Admin bleibt bis 005 fest deutsch, egal was Browser oder Cookie sagen.</summary>
public sealed class AdminLanguageTests
{
    [Theory]
    [InlineData("en", null)]
    [InlineData("en-US,de;q=0.5", null)]
    [InlineData(null, null)]
    [InlineData(null, ".AspNetCore.Culture=c=en|uic=en")]
    public async Task Identity_errors_are_german_regardless_of_browser_language(string? acceptLanguage, string? cookie)
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        using var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        if (acceptLanguage is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (cookie is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        var (_, chooseMethod) = await client.PostAsync($"/Invite?token={token}", "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Tobias", ["Email"] = "admin@example.org" });
        var (_, html) = await client.PostWithTokenAsync("/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = "kurz", ["ConfirmPassword"] = "kurz" },
            FormClient.AntiforgeryToken(chooseMethod));
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("Das Passwort muss mindestens 12 Zeichen lang sein.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("The password must be", text, StringComparison.Ordinal);
    }
}
