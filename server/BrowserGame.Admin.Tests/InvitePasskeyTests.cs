using System.Net;

namespace BrowserGame.Admin.Tests;

/// <summary>Regression (0.2.0 auf dem Pi): Passkey-Schritt meldete „Name/Email field is required“ und verschwieg den Grund.</summary>
public sealed class InvitePasskeyTests
{
    private static async Task<(FormClient Client, string Token, string ChooseMethodHtml)> UpToChooseMethodAsync(AdminFactory factory)
    {
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        var client = factory.CreateFormClient();
        var (_, html) = await client.PostAsync($"/Invite?token={token}", "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Tobias", ["Email"] = "admin@example.org" });
        return (client, token, html);
    }

    [Theory]
    [InlineData("", "kein Passkey übermittelt")]
    [InlineData("{\"id\":\"x\",\"type\":\"public-key\",\"response\":{}}", "Der Passkey konnte nicht angelegt werden (")]
    public async Task Failed_passkey_shows_reason_without_required_field_noise(string credential, string expected)
    {
        await using var factory = new AdminFactory();
        var (client, token, chooseMethod) = await UpToChooseMethodAsync(factory);

        var (response, html) = await client.PostWithTokenAsync("/Invite?handler=Passkey",
            new Dictionary<string, string> { ["Token"] = token, ["Credential"] = credential },
            FormClient.AntiforgeryToken(chooseMethod));
        var text = WebUtility.HtmlDecode(html);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expected, text, StringComparison.Ordinal);
        Assert.DoesNotContain("field is required", text, StringComparison.Ordinal);
        Assert.Contains("Passkey anlegen", text, StringComparison.Ordinal); // man kann es direkt erneut versuchen
        Assert.Contains(factory.Logs.Messages, m => m.StartsWith("Passkey bei der Einrichtung abgelehnt", StringComparison.Ordinal));
    }

    [Fact] // Passwort-Weg darf durch die Änderung nicht leiden
    public async Task Password_step_still_works_without_name_and_email_fields()
    {
        await using var factory = new AdminFactory();
        var (client, token, chooseMethod) = await UpToChooseMethodAsync(factory);

        var (_, html) = await client.PostWithTokenAsync("/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = AdminTestFlows.Password, ["ConfirmPassword"] = AdminTestFlows.Password },
            FormClient.AntiforgeryToken(chooseMethod));

        Assert.Contains("Authenticator-App", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("field is required", html, StringComparison.Ordinal);
    }
}

/// <summary>Regression: Abgebrochene Einrichtung + Neustart darf die E-Mail nicht blockieren.</summary>
public sealed class AbandonedSetupTests
{
    [Fact] // AK-1
    public async Task Restart_after_abandoned_setup_lets_the_same_email_set_up_again()
    {
        await using var factory = new AdminFactory();
        var firstToken = AdminTestFlows.SetupTokenFromLog(factory);
        using (var client = factory.CreateFormClient())
        {
            // Details abgeschickt, dann abgebrochen (z. B. Passkey fehlgeschlagen)
            await client.PostAsync($"/Invite?token={firstToken}", "/Invite?handler=Details",
                new Dictionary<string, string> { ["Token"] = firstToken, ["Name"] = "Tobias", ["Email"] = "admin@example.org" });
        }

        await using var restarted = factory.WithWebHostBuilder(_ => { }); // Neustart beim Update → neuer Einrichtungslink
        _ = restarted.Server;
        var secondToken = AdminTestFlows.SetupTokenFromLog(factory);
        Assert.NotEqual(firstToken, secondToken);

        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, secondToken, "Tobias", "admin@example.org");
        Assert.Equal(10, admin.RecoveryCodes.Count);
    }
}
