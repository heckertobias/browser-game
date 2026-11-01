using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

/// <summary>006 AK-10: Die Passkey-Texte der Account-Seiten folgen der Sprache des Accounts.</summary>
public sealed class PasskeyPagesEnglishTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static readonly Regex NonEnglish = new("[äöüßÄÖÜ]");

    private static void AssertEnglish(string html, string text)
    {
        var visible = HtmlTexts.Visible(html);
        Assert.Contains(text, visible, StringComparison.Ordinal);
        Assert.DoesNotMatch(NonEnglish, visible);
    }

    [Fact]
    public async Task Passkey_texts_are_english_for_english_account()
    {
        using var authenticator = new SoftwareAuthenticator();
        var (client, _) = await AccountTestFlows.SignInAsync(factory, "en");
        using var _ = client;

        var (_, manage) = await client.GetAsync("/Account/Manage");
        var (_, added) = await AccountTestFlows.AddPasskeyAsync(client, authenticator);
        var (_, addFailed) = await client.PostWithTokenAsync(
            "/Account/Manage?handler=AddPasskey", new Dictionary<string, string> { ["Credential"] = "x" }, FormClient.AntiforgeryToken(manage));
        var (_, delete) = await client.GetAsync("/Account/Delete");
        var (_, deleteFailed) = await client.PostWithTokenAsync(
            "/Account/Delete?handler=Passkey", new Dictionary<string, string> { ["Credential"] = "x" }, FormClient.AntiforgeryToken(delete));
        using var anonymous = factory.CreateFormClient();
        anonymous.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        anonymous.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        var (_, login) = await anonymous.GetAsync("/Account/Login");
        var (_, loginFailed) = await anonymous.PostWithTokenAsync(
            "/Account/Login?handler=Passkey", new Dictionary<string, string> { ["Credential"] = "x" }, FormClient.AntiforgeryToken(login));

        AssertEnglish(manage, "Add passkey");
        AssertEnglish(added, "Passkey added.");
        AssertEnglish(addFailed, "The passkey could not be added.");
        AssertEnglish(delete, "Confirm with passkey and delete");
        AssertEnglish(deleteFailed, "The passkey could not be confirmed.");
        AssertEnglish(login, "Sign in with a passkey");
        AssertEnglish(loginFailed, "Signing in with a passkey failed.");
    }
}
