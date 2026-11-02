using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Pages;
using BrowserGame.Data;
using BrowserGame.Server.Tests;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Xunit.Sdk;

namespace BrowserGame.Admin.Tests;

internal static partial class SmtpTestHelpers
{
    public static Dictionary<string, string> Form(FakeSmtpServer server, string? password, string recipient = "t@example.org") => new()
    {
        ["Input.Host"] = "127.0.0.1",
        ["Input.Port"] = server.Port.ToString(CultureInfo.InvariantCulture),
        ["Input.Security"] = nameof(SmtpSecurity.None),
        ["Input.Username"] = server.Username,
        ["Input.Password"] = password ?? "",
        ["Input.FromAddress"] = "noreply@example.net",
        ["Input.FromName"] = "Browser Game",
        ["TestRecipient"] = recipient,
    };

    public static Task<AdminTestFlows.CreatedAdmin> SignedInAsync(AdminFactory factory) =>
        AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

    public static string? TestedPasswordToken(string html)
    {
        var tag = HiddenTokenField().Match(html);
        return tag.Success ? WebUtility.HtmlDecode(TokenValue().Match(tag.Value).Groups[1].Value) : null;
    }

    public static string PasswordStatus(string html)
    {
        var match = StatusBadge().Match(html);
        return match.Success ? match.Groups[1].Value : throw new XunitException("Passwort-Status fehlt.");
    }

    public static IReadOnlyList<string> Alerts(string html, string kind)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "main fehlt");
        return [.. AlertDiv().Matches(html[start..end])
            .Where(m => Regex.IsMatch(m.Value[..(m.Value.IndexOf('>', StringComparison.Ordinal) + 1)], $@"class=""(?:[^""]*\s)?{Regex.Escape(kind)}(?:\s[^""]*)?"""))
            .Select(m => WebUtility.HtmlDecode(Tag().Replace(m.Groups[1].Value, "")).Trim())];
    }

    public static bool FieldInvalid(string html, string name)
    {
        var match = Regex.Match(html, $"<input\\b[^>]*\\bname=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(match.Success, $"Feld {name} fehlt");
        var cls = ClassAttribute().Match(match.Value);
        return cls.Success && cls.Groups[1].Value.Split(' ').Contains("is-invalid");
    }

    public static bool PasswordFieldInvalid(string html) => FieldInvalid(html, "Input.Password");

    public static string PasswordFeedback(string html)
    {
        var match = Regex.Match(html, "<span\\b[^>]*data-valmsg-for=\"Input.Password\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
        Assert.True(match.Success, "Feedback fehlt");
        return WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
    }

    public static ITimeLimitedDataProtector Protector(AdminFactory factory, string purpose = SmtpModel.TestedPasswordPurpose) =>
        factory.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector(purpose).ToTimeLimitedDataProtector();

    public static async Task<SmtpSettings?> StoredAsync(AdminFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().GetAsync();
    }

    public static async Task<List<(string Action, string Details)>> AuditAsync(AdminFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var entries = await scope.ServiceProvider.GetRequiredService<AdminDbContext>().AuditEntries
            .Select(e => new { e.Action, e.Details }).ToListAsync(TestContext.Current.CancellationToken);
        return [.. entries.Select(e => (e.Action, e.Details ?? ""))];
    }

    [GeneratedRegex("""<input\b[^>]*\bname="TestedPassword"[^>]*>""")]
    private static partial Regex HiddenTokenField();

    [GeneratedRegex("""\bvalue="([^"]*)\x22""")]
    private static partial Regex TokenValue();

    [GeneratedRegex("""data-testid="password-status"[^>]*\bdata-status="([^"]+)\x22""")]
    private static partial Regex StatusBadge();

    [GeneratedRegex("<div\\b[^>]*\\bclass=\"(?:[^\"]*\\s)?alert(?:\\s[^\"]*)?\"[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex AlertDiv();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex Tag();

    [GeneratedRegex("""\bclass="([^"]*)\x22""")]
    private static partial Regex ClassAttribute();
}
