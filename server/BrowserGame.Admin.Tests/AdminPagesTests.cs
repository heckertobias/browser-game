using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;

namespace BrowserGame.Admin.Tests;

public sealed partial class AdminPagesTests
{
    [GeneratedRegex("""<(?:input|select|textarea)\b[^>]*\bname="(?<name>[^"]*)"[^>]*>""", RegexOptions.Singleline)]
    private static partial Regex FieldTag();

    [GeneratedRegex(@"<(?:input(?![^>]*type=""hidden"")|select|textarea)\b", RegexOptions.Singleline)]
    private static partial Regex VisibleField();

    [GeneratedRegex("""\bid="([^"]*)\x22""")]
    private static partial Regex IdAttribute();

    [GeneratedRegex("""\baria-describedby="([^"]*)\x22""")]
    private static partial Regex DescribedBy();

    [GeneratedRegex("""\bclass="([^"]*)\x22""")]
    private static partial Regex ClassAttribute();

    private static bool HasClass(string tag, string name) =>
        ClassAttribute().Match(tag) is { Success: true } m && m.Groups[1].Value.Split(' ').Contains(name);

    [Fact] // 005 AK-9 (Handy-Ansicht manuell in T11)
    public async Task Admins_and_audit_lists_are_tables_inside_table_responsive()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        foreach (var (url, testId) in new[] { ("/Admins", "admins-table"), ("/Audit", "audit-table") })
        {
            var (_, html) = await admin.Client.GetAsync(url);
            var table = Regex.Match(html, $"""<table\b[^>]*data-testid="{testId}"[^>]*>""");
            Assert.True(table.Success, url);
            Assert.True(HasClass(table.Value, "table"), url);
            Assert.True(HasClass(table.Value, "table-hover"), url);

            var before = html[..table.Index];
            var wrapper = Regex.Match(before, @"<div\b[^>]*>\s*$");
            Assert.True(wrapper.Success && HasClass(wrapper.Value, "table-responsive"), url);

            var end = html.IndexOf("</table>", table.Index, StringComparison.Ordinal);
            var tableHtml = html[table.Index..end];
            Assert.Contains("<thead>", tableHtml, StringComparison.Ordinal);
            Assert.Contains("<th scope=\"col\">", tableHtml, StringComparison.Ordinal);

            var body = tableHtml[(tableHtml.IndexOf("<tbody>", StringComparison.Ordinal))..];
            var rows = Regex.Matches(body, @"<tr\b.*?</tr>", RegexOptions.Singleline);
            Assert.NotEmpty(rows);
            if (url == "/Admins")
            {
                Assert.Contains(rows, r => r.Value.Contains("Tobias", StringComparison.Ordinal)
                    && Regex.IsMatch(r.Value, """class="[^"]*\bbadge\b"""));
            }            else
            {
                Assert.Contains(rows, r => WebUtility.HtmlDecode(r.Value).Contains(ExpectedText.Get("AuditEntry.FirstAdminSetUp"), StringComparison.Ordinal));
            }
        }
    }

    [Fact] // 005 AK-6 (Optik manuell)
    public async Task Invite_form_has_label_above_field_and_hint_below()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.GetAsync("/Admins");

        AssertLabelAboveFieldAndHintBelow(html, "InviteEmail", expectHint: true);
    }

    [Fact] // 005 AK-6 (Optik manuell)
    public async Task Smtp_form_has_labels_above_fields_and_hints_below()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.GetAsync("/Smtp");
        AssertSmtpForm(html, passwordHint: false);

        var form = new Dictionary<string, string>
        {
            ["Input.Host"] = "127.0.0.1",
            ["Input.Port"] = smtp.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["Input.Security"] = nameof(SmtpSecurity.None),
            ["Input.Username"] = smtp.Username,
            ["Input.Password"] = smtp.Password,
            ["Input.FromAddress"] = "noreply@example.net",
            ["Input.FromName"] = "Browser Game",
        };
        var (_, saved) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", form);
        AssertSmtpForm(saved, passwordHint: true);
    }

    private static void AssertSmtpForm(string html, bool passwordHint)
    {
        foreach (var name in new[] { "Input.Host", "Input.Port", "TestRecipient" })
        {
            AssertLabelAboveFieldAndHintBelow(html, name, expectHint: true);
        }

        foreach (var name in new[] { "Input.Security", "Input.Username", "Input.FromAddress", "Input.FromName" })
        {
            AssertLabelAboveFieldAndHintBelow(html, name, expectHint: false);
        }

        AssertLabelAboveFieldAndHintBelow(html, "Input.Password", expectHint: passwordHint);
    }

    internal static void AssertLabelAboveFieldAndHintBelow(string html, string name, bool expectHint)
    {
        var field = FieldTag().Matches(html).FirstOrDefault(m => m.Groups["name"].Value == name)
            ?? throw new Xunit.Sdk.XunitException($"Feld {name} fehlt.");
        var id = IdAttribute().Match(field.Value).Groups[1].Value;
        Assert.NotEmpty(id);
        Assert.True(HasClass(field.Value, "form-control") || HasClass(field.Value, "form-select"), name);

        var label = Regex.Match(html, $"""<label\b[^>]*\bfor="{Regex.Escape(id)}"[^>]*>""");
        Assert.True(label.Success, name);
        Assert.True(label.Index < field.Index, name);
        Assert.True(HasClass(label.Value, "form-label"), name);
        Assert.False(VisibleField().IsMatch(html[label.Index..field.Index]), name);

        if (!expectHint)
        {
            return;
        }

        var hintId = DescribedBy().Match(field.Value).Groups[1].Value;
        Assert.NotEmpty(hintId);
        var hint = Regex.Match(html, $"""<[a-z]+\b[^>]*\bid="{Regex.Escape(hintId)}"[^>]*>""");
        Assert.True(hint.Success, name);
        Assert.True(hint.Index > field.Index, name);
        Assert.True(HasClass(hint.Value, "form-text"), name);
        var next = VisibleField().Match(html, field.Index + field.Length);
        if (next.Success)
        {
            Assert.True(hint.Index < next.Index, name);
        }
    }
}
