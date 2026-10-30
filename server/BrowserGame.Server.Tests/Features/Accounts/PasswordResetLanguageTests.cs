using System.Net;
using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

/// <summary>006 AK-7, AK-9, AK-10 für die Seiten zum Zurücksetzen des Passworts (003 T8).</summary>
public sealed partial class PasswordResetLanguageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private const string NewPassword = "ein-ganz-neues-passwort";

    private FormClient ClientWith(string acceptLanguage)
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return client;
    }

    private async Task<string> RequestLinkAsync(FormClient client, string accountLanguage)
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory, accountLanguage);
        await AccountTestFlows.ForgotPasswordAsync(client, account.Email);
        return AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(account.Email)).Text);
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static void AssertAll(string html, params string[] parts)
    {
        var text = Decode(html);
        Assert.All(parts, p => Assert.Contains(p, text, StringComparison.Ordinal));
    }

    [Theory] // 006 AK-9
    [InlineData("en", "de", "Reset your password", "Hello ")]
    [InlineData("de", "en", "Passwort zurücksetzen", "Hallo ")]
    public async Task Reset_mail_uses_account_language_not_request_language(string accountLanguage, string requestLanguage, string subject, string greeting)
    {
        using var client = ClientWith(requestLanguage);
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory, accountLanguage);

        await AccountTestFlows.ForgotPasswordAsync(client, account.Email);

        var mail = Assert.Single(factory.Mails.To(account.Email));
        Assert.Equal(subject, mail.Subject);
        Assert.StartsWith(greeting, mail.Text, StringComparison.Ordinal);
    }

    [Fact] // 006 AK-10
    public async Task Forgot_password_is_english()
    {
        using var client = ClientWith("en");

        var (_, form) = await client.GetAsync("/Account/ForgotPassword");
        var (_, empty) = await AccountTestFlows.ForgotPasswordAsync(client, "");
        var (_, sent) = await AccountTestFlows.ForgotPasswordAsync(client, AccountTestFlows.UniqueEmail());

        AssertAll(form, "<title>Forgot password – Browser Game</title>", "Enter your email address and we'll send you a link to set a new password.",
            ">Email<", ">Send link</button>", "Back to sign in");
        AssertAll(empty, "Please enter your email address.");
        AssertAll(sent, "If there is a confirmed account for this address", "The link is valid for 3 hours.", "Back to sign in");
    }

    [Fact] // 006 AK-10
    public async Task Reset_password_is_english()
    {
        using var client = ClientWith("en");
        var link = await RequestLinkAsync(client, "en");

        var (_, form) = await client.GetAsync(link);
        var (_, tooShort) = await AccountTestFlows.ResetPasswordAsync(client, link, "short");
        var (_, done) = await AccountTestFlows.ResetPasswordAsync(client, link, NewPassword);
        var (_, invalid) = await client.GetAsync(link);

        AssertAll(form, "<title>Set new password – Browser Game</title>", "New password (at least 10 characters)", "Repeat new password", ">Save password</button>");
        AssertAll(tooShort, "at least 10 characters");
        AssertAll(done, "Your password has been changed. All other sessions have been signed out. Sign in with your new password.", ">Sign in</a>");
        AssertAll(invalid, "The link is invalid, has expired or has already been used.", "Request a new link");
    }

    [Fact] // 006 AK-10
    public async Task Login_forgot_link_is_english()
    {
        using var client = ClientWith("en");

        var (_, html) = await client.GetAsync("/Account/Login");

        AssertAll(html, "Forgot your password?");
    }

    [Fact] // 006 AK-10
    public async Task Password_reset_pages_have_no_fixed_texts()
    {
        using var german = factory.CreateFormClient();
        using var english = ClientWith("en");
        var deLink = await RequestLinkAsync(german, "de");
        var enLink = await RequestLinkAsync(english, "en");

        var (_, deForgot) = await german.GetAsync("/Account/ForgotPassword");
        var (_, enForgot) = await english.GetAsync("/Account/ForgotPassword");
        var (_, deSent) = await AccountTestFlows.ForgotPasswordAsync(german, AccountTestFlows.UniqueEmail());
        var (_, enSent) = await AccountTestFlows.ForgotPasswordAsync(english, AccountTestFlows.UniqueEmail());
        var (_, deInvalid) = await german.GetAsync("/Account/ResetPassword");
        var (_, enInvalid) = await english.GetAsync("/Account/ResetPassword");
        var (_, deForm) = await german.GetAsync(deLink);
        var (_, enForm) = await english.GetAsync(enLink);
        var (_, deDone) = await AccountTestFlows.ResetPasswordAsync(german, deLink, NewPassword);
        var (_, enDone) = await AccountTestFlows.ResetPasswordAsync(english, enLink, NewPassword);

        AssertNoFixedTexts(deForgot, enForgot);
        AssertNoFixedTexts(deSent, enSent);
        AssertNoFixedTexts(deInvalid, enInvalid);
        AssertNoFixedTexts(deForm, enForm);
        AssertNoFixedTexts(deDone, enDone);
    }

    [Fact] // 006 AK-7
    public async Task Language_switch_on_reset_page_keeps_the_link()
    {
        using var client = factory.CreateFormClient();
        var link = await RequestLinkAsync(client, "de");

        var (_, page) = await client.GetAsync(link);
        var returnUrl = WebUtility.HtmlDecode(ReturnUrlPattern().Match(page).Groups[1].Value);
        var (response, _) = await client.PostAsync(link, "/Language", new Dictionary<string, string> { ["Language"] = "en", ["ReturnUrl"] = returnUrl });
        var (_, html) = await client.GetAsync(link);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(link, returnUrl);
        Assert.Equal(link, response.Headers.Location?.OriginalString);
        Assert.Contains("<html lang=\"en\"", html, StringComparison.Ordinal);
        Assert.Contains("name=\"Password\"", html, StringComparison.Ordinal);
    }

    private static void AssertNoFixedTexts(string deHtml, string enHtml)
    {
        var de = VisibleTexts(deHtml);
        var en = VisibleTexts(enHtml);

        Assert.NotEmpty(de);
        Assert.NotEmpty(en);
        Assert.Empty(en.Intersect(de).Except(SameInBothLanguages));
        Assert.DoesNotContain(en, t => GermanLetters().IsMatch(t));
        Assert.DoesNotContain(en.Concat(de), t => MissingKey().IsMatch(t));
    }

    private static readonly string[] SameInBothLanguages = ["Browser Game", "Deutsch", "English"];

    private static List<string> VisibleTexts(string html)
    {
        var withoutScripts = ScriptPattern().Replace(html, "");
        var nodes = TagPattern().Split(withoutScripts);
        var attributes = TextAttributePattern().Matches(withoutScripts).Select(m => m.Groups[1].Value);
        return nodes.Concat(attributes).Select(t => WebUtility.HtmlDecode(t).Trim()).Where(t => t.Length > 0).ToList();
    }

    [GeneratedRegex("name=\"ReturnUrl\" value=\"([^\"]*)\"")]
    private static partial Regex ReturnUrlPattern();

    [GeneratedRegex("<script.*?</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex TagPattern();

    [GeneratedRegex("(?:placeholder|title|aria-label|alt)=\"([^\"]*)\"")]
    private static partial Regex TextAttributePattern();

    [GeneratedRegex("[äöüÄÖÜß]")]
    private static partial Regex GermanLetters();

    [GeneratedRegex(@"^[A-Z][A-Za-z]*\.[A-Z][A-Za-z]*$")]
    private static partial Regex MissingKey();
}
