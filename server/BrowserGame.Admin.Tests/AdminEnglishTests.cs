using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;
using BrowserGame.Admin.Auth;
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
