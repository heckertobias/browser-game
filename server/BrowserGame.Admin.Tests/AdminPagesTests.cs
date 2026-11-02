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

    [GeneratedRegex("""<p\b[^>]*\bdata-testid="totp-key"[^>]*>([a-z0-9 ]+)</p>""")]
    private static partial Regex TotpKey();

    private static readonly string[] TailwindLeftovers = ["input-bordered", "label-text", "rounded-none", "text-error", "divider", "flex-col", "font-mono"];

    private static void AssertNoTailwindLeftovers(string html)
    {
        var body = html[html.IndexOf("<body", StringComparison.Ordinal)..];
        var classes = ClassAttribute().Matches(body).SelectMany(m => m.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToHashSet();
        foreach (var token in TailwindLeftovers)
        {
            Assert.DoesNotContain(token, classes);
        }
    }

    [Fact] // 005 AK-6 (Optik manuell in T11)
    public async Task Sign_in_and_setup_forms_have_labels_above_fields_and_hints_below()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        using var client = factory.CreateFormClient();

        var (_, login) = await client.GetAsync("/Login");
        AssertLabelAboveFieldAndHintBelow(login, "Email", expectHint: false);
        AssertLabelAboveFieldAndHintBelow(login, "Password", expectHint: false);
        AssertNoTailwindLeftovers(login);

        await AdminTestFlows.LoginPasswordAsync(client, admin.Email);
        var (_, twoFactor) = await client.GetAsync("/LoginTwoFactor");
        AssertLabelAboveFieldAndHintBelow(twoFactor, "Code", expectHint: true);
        AssertNoTailwindLeftovers(twoFactor);

        var token = await AdminTestFlows.InviteTokenAsync(factory);
        var invite = $"/Invite?token={token}";
        using var inviteClient = factory.CreateFormClient();
        var (_, details) = await inviteClient.GetAsync(invite);
        AssertLabelAboveFieldAndHintBelow(details, "Name", expectHint: false);
        AssertLabelAboveFieldAndHintBelow(details, "Email", expectHint: false);
        AssertNoTailwindLeftovers(details);

        var (_, chooseMethod) = await inviteClient.PostAsync(invite, "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Kim", ["Email"] = "kim@example.org" });
        AssertLabelAboveFieldAndHintBelow(chooseMethod, "Password", expectHint: true);
        AssertLabelAboveFieldAndHintBelow(chooseMethod, "ConfirmPassword", expectHint: false);
        AssertNoTailwindLeftovers(chooseMethod);

        var (_, totp) = await inviteClient.PostAsync(invite, "/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = AdminTestFlows.Password, ["ConfirmPassword"] = AdminTestFlows.Password });
        AssertLabelAboveFieldAndHintBelow(totp, "Code", expectHint: false);
        AssertNoTailwindLeftovers(totp);
    }

    [Fact] // 005 AK-6, AK-13
    public async Task Totp_qr_code_stays_on_white_and_recovery_codes_are_a_monospace_list()
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        var invite = $"/Invite?token={token}";
        using var client = factory.CreateFormClient();

        await client.PostAsync(invite, "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Tobias", ["Email"] = "admin@example.org" });
        var (_, totp) = await client.PostAsync(invite, "/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = AdminTestFlows.Password, ["ConfirmPassword"] = AdminTestFlows.Password });

        var qr = Regex.Match(totp, """<div\b[^>]*\bdata-testid="totp-qr"[^>]*>""");
        Assert.True(qr.Success);
        Assert.True(HasClass(qr.Value, "bg-white"));
        var qrEnd = totp.IndexOf("</div>", qr.Index, StringComparison.Ordinal);
        Assert.Contains("<svg", totp[qr.Index..qrEnd], StringComparison.Ordinal);

        var keyMatch = TotpKey().Match(totp);
        Assert.True(keyMatch.Success);
        var keyTag = keyMatch.Value[..(keyMatch.Value.IndexOf('>', StringComparison.Ordinal) + 1)];
        Assert.True(HasClass(keyTag, "font-monospace"));
        Assert.DoesNotContain(" style=\"", totp, StringComparison.Ordinal);
        Assert.DoesNotContain("<style", totp, StringComparison.Ordinal);

        var key = keyMatch.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal);
        var (_, done) = await client.PostAsync(invite, "/Invite?handler=Totp",
            new Dictionary<string, string> { ["Token"] = token, ["Code"] = Totp.Code(key) });

        AssertRecoveryList(done);
        Assert.Contains("data-testid=\"message\"", done, StringComparison.Ordinal);
        Assert.Contains("alert-success", done, StringComparison.Ordinal);
        Assert.Contains("<a href=\"/\"", done, StringComparison.Ordinal);

        var (_, account) = await client.PostAsync("/Account", "/Account?handler=RecoveryCodes", new Dictionary<string, string>());
        AssertRecoveryList(account, expectedCodes: null);
    }

    private static void AssertRecoveryList(string html, int? expectedCodes = 10)
    {
        var list = Regex.Match(html, """<ul\b[^>]*\bdata-testid="recovery-codes"[^>]*>.*?</ul>""", RegexOptions.Singleline);
        Assert.True(list.Success);
        var startTag = list.Value[..(list.Value.IndexOf('>', StringComparison.Ordinal) + 1)];
        Assert.True(HasClass(startTag, "font-monospace"));
        if (expectedCodes is { } count)
        {
            Assert.True(HasClass(startTag, "list-unstyled"));
            Assert.Equal(count, Regex.Count(list.Value, "<li>[^<]+</li>"));
        }
    }
}
