using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>006 T4: Die Spieler-Seiten folgen der Sprache der Anfrage.</summary>
public sealed partial class PlayerPagesEnglishTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private FormClient EnglishClient()
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    private async Task<AccountTestFlows.CreatedAccount> CreateEnglishAccountAsync()
    {
        var account = new AccountTestFlows.CreatedAccount(AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var result = await users.CreateAsync(
            new Account { UserName = account.Name, Email = account.Email, EmailConfirmed = true, Language = "en" }, AccountTestFlows.Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    private static string Decode(string html) => WebUtility.HtmlDecode(html);

    private static void AssertAll(string html, params string[] parts)
    {
        var text = Decode(html);
        Assert.All(parts, p => Assert.Contains(p, text, StringComparison.Ordinal));
    }

    [Fact] // AK-10
    public async Task Home_is_english_for_anonymous_visitors()
    {
        using var client = EnglishClient();

        var (_, html) = await client.GetAsync("/");

        AssertAll(html, "<html lang=\"en\"", "<title>Browser Game</title>",
            "A fantasy world in your browser. No download – just start walking.", ">Play</a>", ">Sign in</a>", ">Sign up</a>");
        AssertAll(html, "href=\"/play\"", "href=\"/Account/Login\"", "href=\"/Account/Register\"");
    }

    [Fact] // AK-10
    public async Task Home_is_english_when_signed_in()
    {
        using var client = EnglishClient();
        var account = await CreateEnglishAccountAsync();
        await AccountTestFlows.LoginAsync(client, account.Email);

        var (_, html) = await client.GetAsync("/");

        AssertAll(html, $"Signed in as {account.Name}", "Sign out");
    }

    [Fact] // AK-10
    public async Task Login_is_english()
    {
        using var client = EnglishClient();

        var (_, html) = await client.GetAsync("/Account/Login");
        var (_, withReturn) = await client.GetAsync("/Account/Login?ReturnUrl=%2Fplay");

        AssertAll(html, "<title>Sign in – Browser Game</title>", ">Email<", ">Password<", ">Sign in</button>",
            "No account yet? Sign up", "Resend confirmation email");
        Assert.DoesNotContain("Sign in to play.", Decode(html), StringComparison.Ordinal);
        AssertAll(withReturn, "Sign in to play.");
    }

    [Fact] // AK-10
    public async Task Wrong_password_shows_the_english_error_only()
    {
        using var client = EnglishClient();
        var account = await CreateEnglishAccountAsync();

        var (_, html) = await AccountTestFlows.LoginAsync(client, account.Email, "falsches-passwort");

        AssertAll(html, "Sign-in failed. Check your email and password. After several failed attempts, sign-in is locked for a short time.");
        Assert.DoesNotContain("Anmeldung fehlgeschlagen", Decode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-10
    public async Task Register_is_english()
    {
        using var client = EnglishClient();

        var (_, html) = await client.GetAsync("/Account/Register");

        AssertAll(html, "<title>Sign up – Browser Game</title>", ">Email<", "Player name (3 to 16 letters A–Z)",
            "Password (at least 10 characters)", "Repeat password", ">Sign up</button>", "Already have an account? Sign in");
    }

    [Fact] // AK-10
    public async Task Password_mismatch_is_explained_in_english()
    {
        using var client = EnglishClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(
            client, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName(), confirm: "ein-anderes-passwort");

        Assert.Equal(1, Occurrences(Decode(html), "The passwords do not match."));
    }

    [Fact] // AK-10
    public async Task Password_mismatch_is_explained_in_german_by_default()
    {
        using var client = factory.CreateFormClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(
            client, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName(), confirm: "ein-anderes-passwort");

        Assert.Equal(1, Occurrences(Decode(html), "Die Passwörter stimmen nicht überein."));
    }

    [Fact] // AK-10
    public async Task Successful_registration_is_confirmed_in_english()
    {
        using var client = EnglishClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());

        AssertAll(html, "Almost done! We've sent you an email. Click the link in it to confirm your email address.",
            "Didn't get an email? Send it again", "href=\"/Account/ResendConfirmation\"");
    }

    [Fact] // AK-10
    public async Task Email_confirmation_is_english()
    {
        using var client = EnglishClient();
        var email = AccountTestFlows.UniqueEmail();

        var (_, invalid) = await client.GetAsync("/Account/ConfirmEmail");
        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());
        var (_, confirmed) = await client.GetAsync(AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(email)).Text));

        AssertAll(invalid, "<title>Confirm email – Browser Game</title>", "The link is invalid or has expired.", "Resend confirmation email");
        AssertAll(confirmed, "Your email address is confirmed.", "Play now");
    }

    [Fact] // AK-10
    public async Task Resend_confirmation_is_english()
    {
        using var client = EnglishClient();

        var (_, page) = await client.GetAsync("/Account/ResendConfirmation");
        var (_, sent) = await client.PostAsync(
            "/Account/ResendConfirmation", "/Account/ResendConfirmation", new Dictionary<string, string> { ["Email"] = AccountTestFlows.UniqueEmail() });

        AssertAll(page, "<title>Confirmation email – Browser Game</title>", "Send email");
        AssertAll(sent, "If there is an unconfirmed account for this address, we've sent you a new email.");
    }

    [Fact] // AK-10
    public async Task Sign_out_leads_to_the_english_home()
    {
        using var client = EnglishClient();
        var account = await CreateEnglishAccountAsync();
        await AccountTestFlows.LoginAsync(client, account.Email);
        AssertAll((await client.GetAsync("/")).Html, "Signed in as");

        var (logout, _) = await client.PostAsync("/", "/Account/Logout", new Dictionary<string, string>());
        var (_, html) = await client.GetAsync(logout.Headers.Location!.OriginalString);

        AssertAll(html, "Sign in");
        Assert.DoesNotContain("Signed in as", Decode(html), StringComparison.Ordinal);
    }

    [Theory] // AK-10
    [InlineData("/")]
    [InlineData("/Account/Login")]
    [InlineData("/Account/Login?ReturnUrl=%2Fplay")]
    [InlineData("/Account/Register")]
    [InlineData("/Account/ResendConfirmation")]
    [InlineData("/Account/ConfirmEmail")]
    public async Task Player_pages_have_no_fixed_texts(string url)
    {
        using var german = factory.CreateFormClient();
        using var english = EnglishClient();

        var (deResponse, deHtml) = await german.GetAsync(url);
        var (enResponse, enHtml) = await english.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, deResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, enResponse.StatusCode);
        AssertNoFixedTexts(deHtml, enHtml);
    }

    [Fact] // AK-10
    public async Task Signed_in_home_has_no_fixed_texts()
    {
        using var german = factory.CreateFormClient();
        using var english = EnglishClient();
        await AccountTestFlows.LoginAsync(german, (await AccountTestFlows.CreateConfirmedAccountAsync(factory)).Email);
        await AccountTestFlows.LoginAsync(english, (await CreateEnglishAccountAsync()).Email);

        var (_, deHtml) = await german.GetAsync("/");
        var (_, enHtml) = await english.GetAsync("/");

        Assert.Contains("Angemeldet als", Decode(deHtml), StringComparison.Ordinal);
        Assert.Contains("Signed in as", Decode(enHtml), StringComparison.Ordinal);
        AssertNoFixedTexts(deHtml, enHtml);
    }

    [Fact] // AK-10
    public async Task Resend_and_registration_results_have_no_fixed_texts()
    {
        using var german = factory.CreateFormClient();
        using var english = EnglishClient();
        var resend = new Dictionary<string, string> { ["Email"] = AccountTestFlows.UniqueEmail() };

        var (_, deSent) = await german.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", resend);
        var (_, enSent) = await english.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", resend);
        var (_, deRegistered) = await AccountTestFlows.RegisterAsync(german, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());
        var (_, enRegistered) = await AccountTestFlows.RegisterAsync(english, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());

        Assert.Contains("Falls es zu dieser Adresse", Decode(deSent), StringComparison.Ordinal);
        Assert.Contains("If there is an unconfirmed account", Decode(enSent), StringComparison.Ordinal);
        Assert.Contains("Fast geschafft!", Decode(deRegistered), StringComparison.Ordinal);
        Assert.Contains("Almost done!", Decode(enRegistered), StringComparison.Ordinal);
        AssertNoFixedTexts(deSent, enSent);
        AssertNoFixedTexts(deRegistered, enRegistered);
    }

    [Fact] // AK-10
    public async Task Confirmed_email_page_has_no_fixed_texts()
    {
        using var german = factory.CreateFormClient();
        using var english = EnglishClient();
        var deEmail = AccountTestFlows.UniqueEmail();
        var enEmail = AccountTestFlows.UniqueEmail();
        await AccountTestFlows.RegisterAsync(german, deEmail, AccountTestFlows.UniqueName());
        await AccountTestFlows.RegisterAsync(english, enEmail, AccountTestFlows.UniqueName());

        var (_, deHtml) = await german.GetAsync(AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(deEmail)).Text));
        var (_, enHtml) = await english.GetAsync(AccountTestFlows.LinkFromMail(Assert.Single(factory.Mails.To(enEmail)).Text));

        Assert.Contains("bestätigt", Decode(deHtml), StringComparison.Ordinal);
        Assert.Contains("Your email address is confirmed.", Decode(enHtml), StringComparison.Ordinal);
        AssertNoFixedTexts(deHtml, enHtml);
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

    private static int Occurrences(string text, string part) =>
        text.Split(part, StringSplitOptions.None).Length - 1;

    private static List<string> VisibleTexts(string html)
    {
        var withoutScripts = ScriptPattern().Replace(html, "");
        var nodes = TagPattern().Split(withoutScripts);
        var attributes = TextAttributePattern().Matches(withoutScripts).Select(m => m.Groups[1].Value);
        return nodes.Concat(attributes).Select(t => WebUtility.HtmlDecode(t).Trim()).Where(t => t.Length > 0).ToList();
    }

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
