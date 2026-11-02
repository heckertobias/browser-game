using BrowserGame.Server.Tests;
using Microsoft.AspNetCore.DataProtection;
using static BrowserGame.Admin.Tests.SmtpTestHelpers;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpTestedPasswordTests
{
    private const string Save = "/Smtp?handler=Save";
    private const string Test = "/Smtp?handler=Test";

    private static async Task<(AdminTestFlows.CreatedAdmin Admin, string Old)> StoredWithOldPasswordAsync(AdminFactory factory, FakeSmtpServer smtp)
    {
        var admin = await SignedInAsync(factory);
        await admin.Client.PostAsync("/Smtp", Save, Form(smtp, "alt-000"));
        return (admin, "alt-000");
    }

    private static Dictionary<string, string> WithToken(Dictionary<string, string> form, string token)
    {
        form["TestedPassword"] = token;
        return form;
    }

    [Fact] // AK-15
    public async Task Tested_password_is_saved_when_the_field_is_left_empty()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, tested) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        Assert.Single(Alerts(tested, "alert-success"));
        var token = TestedPasswordToken(tested);
        Assert.NotNull(token);
        Assert.DoesNotContain(smtp.Password, tested, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"tested-password-hint\"", tested, StringComparison.Ordinal);

        var (_, saved) = await admin.Client.PostAsync("/Smtp", Save, WithToken(Form(smtp, null), token));
        Assert.Equal(smtp.Password, (await StoredAsync(factory))!.Password);
        Assert.Single(Alerts(saved, "alert-success"));
        Assert.Null(TestedPasswordToken(saved));
        Assert.Equal("Stored", PasswordStatus(saved));

        var audit = await AuditAsync(factory);
        Assert.Contains(audit, e => e.Action == ExpectedText.Get("AuditEntry.SmtpChanged")
            && e.Details == ExpectedText.Format("AuditEntry.SmtpChangedFields", "Password"));
        Assert.DoesNotContain(audit, e => e.Details.Contains(smtp.Password, StringComparison.Ordinal) || e.Details.Contains(token, StringComparison.Ordinal));
    }

    [Fact] // AK-15
    public async Task Token_is_issued_only_after_a_successful_test_with_a_new_password()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, failed) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, "falsch"));
        Assert.Null(TestedPasswordToken(failed));

        await admin.Client.PostAsync("/Smtp", Save, Form(smtp, smtp.Password));
        var (_, withStored) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, null));
        Assert.Single(Alerts(withStored, "alert-success"));
        Assert.Null(TestedPasswordToken(withStored));
    }

    [Fact] // AK-15
    public async Task Typed_password_takes_precedence_over_the_tested_one()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, tested) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        await admin.Client.PostAsync("/Smtp", Save, WithToken(Form(smtp, "getippt-1"), TestedPasswordToken(tested)!));

        Assert.Equal("getippt-1", (await StoredAsync(factory))!.Password);
    }

    [Fact] // AK-15
    public async Task Second_test_with_empty_field_uses_the_tested_password_and_renews_it()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, first) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        var (_, second) = await admin.Client.PostAsync("/Smtp", Test, WithToken(Form(smtp, null), TestedPasswordToken(first)!));

        Assert.Single(Alerts(second, "alert-success"));
        Assert.Equal(2, smtp.Messages.Count);
        Assert.NotNull(TestedPasswordToken(second));
    }

    [Fact] // AK-15
    public async Task Failed_test_keeps_the_last_tested_password()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, first) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        var token = TestedPasswordToken(first)!;
        var (_, failed) = await admin.Client.PostAsync("/Smtp", Test, WithToken(Form(smtp, "falsch"), token));
        Assert.Single(Alerts(failed, "alert-danger"));
        Assert.Equal(token, TestedPasswordToken(failed));

        await admin.Client.PostAsync("/Smtp", Save, WithToken(Form(smtp, null), token));
        Assert.Equal(smtp.Password, (await StoredAsync(factory))!.Password);
    }

    [Fact] // AK-15
    public async Task Tested_password_expires_after_30_minutes()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var before = DateTimeOffset.UtcNow;
        var (_, tested) = await admin.Client.PostAsync("/Smtp", Test, Form(smtp, smtp.Password));
        var after = DateTimeOffset.UtcNow;

        Assert.Equal(smtp.Password, Protector(factory).Unprotect(TestedPasswordToken(tested)!, out var expiration));
        var lifetime = TimeSpan.FromMinutes(30);
        var tolerance = TimeSpan.FromSeconds(1);
        Assert.InRange(expiration, before + lifetime - tolerance, after + lifetime + tolerance);
    }

    [Theory] // AK-15
    [InlineData("tampered")]
    [InlineData("expired")]
    [InlineData("other-purpose")]
    [InlineData("garbage")]
    public async Task Invalid_token_saves_nothing_and_marks_the_password_field(string kind)
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, old) = await StoredWithOldPasswordAsync(factory, smtp);

        var token = kind switch
        {
            "tampered" => Tamper(Protector(factory).Protect(smtp.Password, TimeSpan.FromMinutes(30))),
            "expired" => Protector(factory).Protect(smtp.Password, DateTimeOffset.UtcNow.AddMinutes(-1)),
            "other-purpose" => Protector(factory, "Admin.Smtp.Other").Protect(smtp.Password, TimeSpan.FromMinutes(30)),
            _ => "kein-token",
        };
        var form = WithToken(Form(smtp, null), token);
        form["Input.FromName"] = "Changed";
        var (_, html) = await admin.Client.PostAsync("/Smtp", Save, form);

        var stored = (await StoredAsync(factory))!;
        Assert.Equal(old, stored.Password);
        Assert.Equal("Browser Game", stored.FromName);
        Assert.True(PasswordFieldInvalid(html));
        Assert.Equal(ExpectedText.Get("Smtp.Error.TestedPasswordInvalid"), PasswordFeedback(html));
        Assert.Empty(Alerts(html, "alert-success"));
        Assert.Null(TestedPasswordToken(html));
        Assert.Single(await AuditAsync(factory), e => e.Action == ExpectedText.Get("AuditEntry.SmtpChanged"));
    }

    [Fact] // AK-15
    public async Task Invalid_token_in_test_mail_sends_nothing()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var (admin, _) = await StoredWithOldPasswordAsync(factory, smtp);

        var (_, html) = await admin.Client.PostAsync("/Smtp", Test, WithToken(Form(smtp, null), "kein-token"));

        Assert.Empty(smtp.Messages);
        Assert.True(PasswordFieldInvalid(html));
        Assert.Equal(ExpectedText.Get("Smtp.Error.TestedPasswordInvalid"), PasswordFeedback(html));
    }

    private static string Tamper(string token)
    {
        var i = token.Length / 2;
        return token[..i] + (token[i] == 'A' ? 'B' : 'A') + token[(i + 1)..];
    }
}
