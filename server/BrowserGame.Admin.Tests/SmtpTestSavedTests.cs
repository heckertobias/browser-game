using System.Globalization;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Tests;
using static BrowserGame.Admin.Tests.SmtpTestHelpers;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpTestSavedTests
{
    private const string Save = "/Smtp?handler=Save";
    private const string TestSaved = "/Smtp?handler=TestSaved";

    [Fact] // AK-17
    public async Task Test_with_saved_settings_uses_the_saved_server_not_the_form()
    {
        await using var factory = new AdminFactory();
        await using var saved = new FakeSmtpServer();
        await using var other = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(saved, saved.Password));

        var (_, html) = await admin.Client.PostAsync("/Smtp", TestSaved, Form(other, "falsch"));

        Assert.Contains($"Subject: {ExpectedText.Get("Mail.Test.Subject")}", Assert.Single(saved.Messages), StringComparison.Ordinal);
        Assert.Empty(other.Messages);
        Assert.Equal([ExpectedText.Format("Smtp.TestSavedSent", "t@example.org")], Alerts(html, "alert-success"));
        Assert.Contains($"value=\"{other.Port}\"", html, StringComparison.Ordinal);
        Assert.Equal(saved.Port, (await StoredAsync(factory))!.Port);
        Assert.Contains(await AuditAsync(factory), e => e.Action == ExpectedText.Get("AuditEntry.TestMailSavedSent")
            && e.Details == ExpectedText.Format("AuditEntry.To", "t@example.org"));
    }

    [Fact] // AK-17
    public async Task Test_with_saved_settings_ignores_form_validation()
    {
        await using var factory = new AdminFactory();
        await using var saved = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(saved, saved.Password));

        var form = Form(saved, null);
        form["Input.Host"] = "mx.mailhost.example:993";
        form["Input.Port"] = "993";
        form["Input.FromAddress"] = "";
        var (_, html) = await admin.Client.PostAsync("/Smtp", TestSaved, form);

        Assert.Single(saved.Messages);
        Assert.DoesNotContain("is-invalid", html, StringComparison.Ordinal);
        Assert.Empty(Alerts(html, "alert-danger"));
    }

    [Fact] // AK-17
    public async Task Test_with_saved_settings_is_offered_only_when_settings_are_saved()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, empty) = await admin.Client.GetAsync("/Smtp");
        Assert.DoesNotContain("handler=TestSaved", empty, StringComparison.Ordinal);

        var (_, html) = await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));
        var button = Regex.Match(html, """<button\b[^>]*data-testid="test-saved"[^>]*>""");
        Assert.True(button.Success);
        Assert.Contains("handler=TestSaved", button.Value, StringComparison.Ordinal);
        Assert.Contains("formnovalidate", button.Value, StringComparison.Ordinal);
    }

    [Fact] // AK-17
    public async Task Test_with_saved_settings_checks_the_recipient()
    {
        await using var factory = new AdminFactory();
        await using var saved = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(saved, saved.Password));

        var (_, html) = await admin.Client.PostAsync("/Smtp", TestSaved, Form(saved, null, recipient: "kein-mail"));

        Assert.True(FieldInvalid(html, "TestRecipient"));
        Assert.Empty(saved.Messages);
    }

    [Fact] // AK-17
    public async Task Test_with_saved_settings_reports_mail_errors()
    {
        await using var factory = new AdminFactory();
        await using var saved = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(saved, "falsch"));

        var (_, html) = await admin.Client.PostAsync("/Smtp", TestSaved, Form(saved, null));

        var alert = Assert.Single(Alerts(html, "alert-danger"));
        Assert.Contains(MailErrors.ResourceManager.GetString(nameof(MailErrors.AuthenticationFailed), CultureInfo.InvariantCulture)!, alert, StringComparison.Ordinal);
        var prefix = ExpectedText.Format("AuditEntry.ToFailed", "t@example.org", "");
        Assert.Contains(await AuditAsync(factory), e => e.Action == ExpectedText.Get("AuditEntry.TestMailSavedSent")
            && e.Details.StartsWith(prefix, StringComparison.Ordinal));
    }
}
