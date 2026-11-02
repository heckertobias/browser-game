using System.Collections;
using System.Globalization;
using System.Net;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;
using BrowserGame.Admin.Auth;
using Microsoft.AspNetCore.Identity;
using BrowserGame.Data;
using MimeKit;

namespace BrowserGame.Admin.Tests;

public sealed partial class AdminEnglishTests
{
    private static string PagesDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BrowserGame.slnx")))
                return Path.Combine(dir.FullName, "BrowserGame.Admin", "Pages");
        throw new InvalidOperationException("BrowserGame.slnx nicht gefunden.");
    }

    [GeneratedRegex("""\bt\["([^"]+)\x22""")]
    private static partial Regex PageModelTextKey();

    [GeneratedRegex("""<input\b[^>]*data-testid="invite-link"[^>]*>""")]
    private static partial Regex InviteLinkInput();

    [Fact] // 005 AK-14
    public async Task Invite_mail_is_english()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().SaveAsync(
                new SmtpSettings("127.0.0.1", smtp.Port, SmtpSecurity.None, smtp.Username, smtp.Password, "noreply@example.net", null),
                TestContext.Current.CancellationToken);
        }

        admin.Client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "de-DE,de;q=0.9");
        var (_, html) = await admin.Client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "neu@example.org" });

        var input = InviteLinkInput().Match(html).Value;
        var link = WebUtility.HtmlDecode(Regex.Match(input, """\bvalue="([^"]+)\x22""").Groups[1].Value);
        Assert.NotEmpty(link);

        var raw = Assert.Single(smtp.Messages);
        var message = await MimeMessage.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(raw)), TestContext.Current.CancellationToken);
        Assert.Equal(ExpectedText.Get("Mail.Invite.Subject"), message.Subject);
        Assert.Equal(
            ExpectedText.Format("Mail.Invite.Body", "Tobias", link, AdminInvites.Lifetime.TotalHours).ReplaceLineEndings("\n"),
            message.TextBody!.ReplaceLineEndings("\n").TrimEnd());
        Assert.Contains($"href=\"{link}\"", message.HtmlBody!, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-14
    public async Task Test_mail_is_english()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        admin.Client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "de-DE,de;q=0.9");

        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", ValidSmtpForm(smtp, smtp.Password));

        var raw = Assert.Single(smtp.Messages);
        var message = await MimeMessage.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(raw)), TestContext.Current.CancellationToken);
        Assert.Equal(ExpectedText.Get("Mail.Test.Subject"), message.Subject);
        Assert.Equal(ExpectedText.Get("Mail.Test.Body"), message.TextBody!.ReplaceLineEndings("\n").TrimEnd());
    }

    private static Dictionary<string, string> ValidSmtpForm(FakeSmtpServer smtp, string? password) => new()
    {
        ["Input.Host"] = "127.0.0.1",
        ["Input.Port"] = smtp.Port.ToString(CultureInfo.InvariantCulture),
        ["Input.Security"] = nameof(SmtpSecurity.None),
        ["Input.Username"] = smtp.Username,
        ["Input.Password"] = password ?? "",
        ["Input.FromAddress"] = "noreply@example.net",
        ["Input.FromName"] = "Browser Game",
        ["TestRecipient"] = "t@example.org",
    };

    // Bekannte Grenze: ein wörtlicher englischer Text, der zufällig einem resx-Wert gleicht, fällt nicht auf.
    // Ziel ist „nichts außerhalb der resx“.
    private static readonly string[] TestData = ["admin@example.org", "t@example.org", "Tobias"];

    private static readonly Regex[] KnownTexts = [.. Values(AdminTexts.ResourceManager).Concat(Values(MailErrors.ResourceManager))
        .OrderByDescending(v => v.Length)
        .Select(ToPattern)];

    // Ein Platzhalter am Ende steht für ein Wort oder eine Kommaliste ohne Leerzeichen im Eintrag (z. B. Feldnamen).
    private static Regex ToPattern(string value)
    {
        var pattern = Placeholder().Replace(Regex.Escape(value), ".+?");
        if (pattern.EndsWith(".+?", StringComparison.Ordinal))
        {
            pattern = pattern[..^3] + @"[^\s,]*(?:,\s[^\s,]+)*";
        }

        return new Regex(pattern, RegexOptions.Singleline);
    }

    private static IEnumerable<string> Values(ResourceManager manager) =>
        manager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false)!
            .Cast<DictionaryEntry>().Select(e => (string)e.Value!);

    [GeneratedRegex(@"\\\{\d+\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("""<script\b.*?</script>""", RegexOptions.Singleline)]
    private static partial Regex ScriptBlock();

    [GeneratedRegex("""<ul\b[^>]*data-testid="recovery-codes".*?</ul>""", RegexOptions.Singleline)]
    private static partial Regex RecoveryCodeList();

    [GeneratedRegex("""\b(?:placeholder|title|aria-label|alt)="([^"]*)\x22""")]
    private static partial Regex TextAttribute();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\p{L}")]
    private static partial Regex Letter();

    private static void AssertOnlyKnownTexts(string step, string html)
    {
        var body = html[html.IndexOf("<body", StringComparison.Ordinal)..];
        body = RecoveryCodeList().Replace(ScriptBlock().Replace(body, ""), "");
        var texts = TextAttribute().Matches(body).Select(m => m.Groups[1].Value)
            .Concat(Tag().Split(body))
            .Select(t => WebUtility.HtmlDecode(t).Trim())
            .Where(t => t.Length > 0)
            .ToList();
        Assert.NotEmpty(texts);

        foreach (var text in texts)
        {
            var rest = text;
            foreach (var data in TestData.OrderByDescending(d => d.Length))
            {
                rest = rest.Replace(data, " ", StringComparison.Ordinal);
            }

            foreach (var known in KnownTexts)
            {
                rest = known.Replace(rest, " ");
            }

            Assert.False(Letter().IsMatch(rest), $"{step}: Text nicht aus der resx: \"{text}\"");
        }
    }

    [Fact] // 005 AK-14, AK-13: T8 ergänzt Login, LoginTwoFactor und Invite
    public async Task Signed_in_pages_show_only_texts_from_resources()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        admin.Client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "de-DE,de;q=0.9");
        admin.Client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", ".AspNetCore.Culture=c=de|uic=de");
        var client = admin.Client;

        foreach (var url in new[] { "/", "/Smtp", "/Admins", "/Account" })
        {
            AssertOnlyKnownTexts($"GET {url}", (await client.GetAsync(url)).Html);
        }

        var badHost = ValidSmtpForm(smtp, smtp.Password);
        badHost["Input.Host"] = "mx.mailhost.example:993";
        badHost["Input.Port"] = "993";
        AssertOnlyKnownTexts("Save mit Port im Host", (await client.PostAsync("/Smtp", "/Smtp?handler=Save", badHost)).Html);
        AssertOnlyKnownTexts("Test mit falschem Passwort", (await client.PostAsync("/Smtp", "/Smtp?handler=Test", ValidSmtpForm(smtp, "falsch"))).Html);
        AssertOnlyKnownTexts("Test erfolgreich", (await client.PostAsync("/Smtp", "/Smtp?handler=Test", ValidSmtpForm(smtp, smtp.Password))).Html);
        AssertOnlyKnownTexts("Save gültig", (await client.PostAsync("/Smtp", "/Smtp?handler=Save", ValidSmtpForm(smtp, smtp.Password))).Html);

        AssertOnlyKnownTexts("Einladung ohne Mail",
            (await client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "" })).Html);
        AssertOnlyKnownTexts("Einladung mit ungültiger Mail",
            (await client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "kein-mail" })).Html);

        string ownId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            ownId = (await scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>().FindByEmailAsync("admin@example.org"))!.Id;
        }

        AssertOnlyKnownTexts("Sich selbst entfernen",
            (await client.PostAsync("/Admins", "/Admins?handler=Remove", new Dictionary<string, string> { ["RemoveId"] = ownId })).Html);
        AssertOnlyKnownTexts("Passkey ohne Credential",
            (await client.PostAsync("/Account", "/Account?handler=AddPasskey", new Dictionary<string, string>())).Html);
        AssertOnlyKnownTexts("Neue Wiederherstellungscodes",
            (await client.PostAsync("/Account", "/Account?handler=RecoveryCodes", new Dictionary<string, string>())).Html);

        AssertOnlyKnownTexts("GET /Audit", (await client.GetAsync("/Audit")).Html);
    }

    [Fact] // 005 AK-14
    public void Page_models_use_only_existing_text_keys()
    {
        var keys = Directory.EnumerateFiles(PagesDirectory(), "*.cshtml.cs", SearchOption.AllDirectories)
            .SelectMany(file => PageModelTextKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.True(AdminTexts.ResourceManager.GetString(key, System.Globalization.CultureInfo.InvariantCulture) is not null, key);
        }
    }
}
