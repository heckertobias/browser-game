using BrowserGame.Server.Tests;
using static BrowserGame.Admin.Tests.SmtpTestHelpers;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpNoLoginTests
{
    private const string Save = "/Smtp?handler=Save";
    private const string Test = "/Smtp?handler=Test";
    private const string TestSaved = "/Smtp?handler=TestSaved";

    private static Dictionary<string, string> NoLogin(FakeSmtpServer smtp, string? password)
    {
        var form = Form(smtp, password);
        form["Input.Username"] = "";
        return form;
    }

    [Fact] // 005 AK-18
    public async Task Saving_without_username_removes_the_password_and_shows_no_login()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));

        var (_, saved) = await admin.Client.PostAsync("/Smtp", Save, NoLogin(smtp, null));

        var stored = (await StoredAsync(factory))!;
        Assert.Null(stored.Username);
        Assert.Null(stored.Password);
        Assert.Equal("NoLogin", PasswordStatus(saved));
        Assert.Empty(Alerts(saved, "alert-warning"));
        Assert.Single(Alerts(saved, "alert-success"));
        Assert.Contains("data-testid=\"no-login-hint\"", saved, StringComparison.Ordinal);
        Assert.Contains(await AuditAsync(factory), e => e.Action == ExpectedText.Get("AuditEntry.SmtpChanged")
            && e.Details == ExpectedText.Format("AuditEntry.SmtpChangedFields", "Username, Password"));

        var (_, page) = await admin.Client.GetAsync("/Smtp");
        Assert.Equal("NoLogin", PasswordStatus(page));
        Assert.Empty(Alerts(page, "alert-warning"));
        Assert.Contains("data-testid=\"no-login-hint\"", page, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-18
    public async Task Password_typed_without_username_is_not_saved()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.PostAsync("/Smtp", Save, NoLogin(smtp, "egal-123"));

        Assert.Null((await StoredAsync(factory))!.Password);
        Assert.Equal("NoLogin", PasswordStatus(html));
        Assert.Contains("data-testid=\"no-login-hint\"", html, StringComparison.Ordinal);
        Assert.Empty(Alerts(html, "alert-warning"));
        Assert.DoesNotContain("egal-123", html, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-18
    public async Task Without_username_mail_is_sent_without_signing_in()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, NoLogin(smtp, null));

        var (_, saved) = await admin.Client.PostAsync("/Smtp", TestSaved, NoLogin(smtp, null));
        Assert.Equal([ExpectedText.Format("Smtp.TestSavedSent", "t@example.org")], Alerts(saved, "alert-success"));
        Assert.Single(smtp.Messages);

        var (_, tested) = await admin.Client.PostAsync("/Smtp", Test, NoLogin(smtp, "egal-123"));
        Assert.Single(Alerts(tested, "alert-success"));
        Assert.Equal(2, smtp.Messages.Count);
        Assert.Equal(0, smtp.AuthAttempts);
    }

    [Fact] // 005 AK-18
    public async Task Test_without_username_issues_no_token_and_drops_a_tested_one()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        var (_, first) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        var token = TestedPasswordToken(first);
        Assert.NotNull(token);

        var form = NoLogin(smtp, null);
        form["TestedPassword"] = token;
        var (_, html) = await admin.Client.PostAsync("/Smtp", Test, form);

        Assert.Single(Alerts(html, "alert-success"));
        Assert.Null(TestedPasswordToken(html));
        Assert.DoesNotContain("data-testid=\"tested-password-hint\"", html, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-18
    public async Task Saving_without_username_ignores_tested_password()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));

        var form = NoLogin(smtp, null);
        form["TestedPassword"] = "kein-token";
        var (_, html) = await admin.Client.PostAsync("/Smtp", Save, form);

        Assert.Single(Alerts(html, "alert-success"));
        Assert.False(PasswordFieldInvalid(html));
        Assert.Null(TestedPasswordToken(html));
        Assert.Null((await StoredAsync(factory))!.Password);
    }

    [Fact] // 005 AK-18, T9 Entscheidung 10
    public async Task Form_errors_keep_the_tested_password_without_username()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        var (_, first) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        var token = TestedPasswordToken(first);
        Assert.NotNull(token);

        var form = NoLogin(smtp, null);
        form["Input.Host"] = "mx.mailhost.example:993";
        form["TestedPassword"] = token;
        var (_, html) = await admin.Client.PostAsync("/Smtp", Save, form);

        Assert.True(FieldInvalid(html, "Input.Host"));
        Assert.Equal(token, TestedPasswordToken(html));
    }
}
