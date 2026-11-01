using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using BrowserGame.Server.Tests;

namespace BrowserGame.Admin.Tests;

/// <summary>005 AK-7, AK-8: Fehler am Feld bzw. oben im Kasten; Erfolg und Hinweis als Meldung über dem Formular.</summary>
public sealed partial class AdminMessagesTests
{
    private sealed record Alert(int Index, string[] Classes, string StartTag, string Text);

    private static Task<AdminTestFlows.CreatedAdmin> SignedInAsync(AdminFactory factory) =>
        AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

    private static Dictionary<string, string> SmtpForm(FakeSmtpServer server, string password) => new()
    {
        ["Input.Host"] = "127.0.0.1",
        ["Input.Port"] = server.Port.ToString(CultureInfo.InvariantCulture),
        ["Input.Security"] = nameof(SmtpSecurity.None),
        ["Input.Username"] = server.Username,
        ["Input.Password"] = password,
        ["Input.FromAddress"] = "noreply@example.net",
        ["Input.FromName"] = "Browser Game",
        ["TestRecipient"] = "t@example.org",
    };

    private static string MainContent(string html)
    {
        var start = html.IndexOf("<main", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start, "main fehlt");
        return html[start..end];
    }

    private static string[] Classes(string startTag)
    {
        var match = ClassAttribute().Match(startTag);
        return match.Success ? match.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) : [];
    }

    private static Match Field(string html, string name)
    {
        var match = Regex.Match(html, $"<(?:input|select|textarea)\\b[^>]*\\bname=\"{Regex.Escape(name)}\"[^>]*>");
        Assert.True(match.Success, $"Feld {name} fehlt");
        return match;
    }

    private static (int Index, string[] Classes, string Text) Feedback(string html, string name)
    {
        var match = Regex.Match(html, $"<span\\b[^>]*\\bdata-valmsg-for=\"{Regex.Escape(name)}\"[^>]*>(.*?)</span>", RegexOptions.Singleline);
        Assert.True(match.Success, $"Feedback für {name} fehlt");
        var startTag = match.Value[..(match.Value.IndexOf('>', StringComparison.Ordinal) + 1)];
        return (match.Index, Classes(startTag), WebUtility.HtmlDecode(match.Groups[1].Value).Trim());
    }

    private static void AssertErrorAtField(string html, string name)
    {
        var field = Field(html, name);
        Assert.Contains("is-invalid", Classes(field.Value));
        var feedback = Feedback(html, name);
        Assert.Contains("invalid-feedback", feedback.Classes);
        Assert.NotEmpty(feedback.Text);
        var fieldEnd = field.Index + field.Length;
        Assert.True(feedback.Index >= fieldEnd, $"Feedback von {name} steht vor dem Feld");
        Assert.DoesNotMatch(FieldStart(), html[fieldEnd..feedback.Index]);
        Assert.DoesNotMatch(ClosingContainer(), html[fieldEnd..feedback.Index]);
    }

    private static void AssertNoErrorAtField(string html, string name)
    {
        Assert.DoesNotContain("is-invalid", Classes(Field(html, name).Value));
        Assert.Empty(Feedback(html, name).Text);
    }

    private static List<Alert> Alerts(string html) =>
        AlertDiv().Matches(html)
            .Select(m => new Alert(
                m.Index,
                Classes(m.Value[..(m.Value.IndexOf('>', StringComparison.Ordinal) + 1)]),
                m.Value[..(m.Value.IndexOf('>', StringComparison.Ordinal) + 1)],
                WebUtility.HtmlDecode(AnyTag().Replace(m.Groups[1].Value, " ")).Trim()))
            .ToList();

    private static List<Alert> Alerts(string html, string kind) => Alerts(html).Where(a => a.Classes.Contains(kind)).ToList();

    private static int FormStart(string main) => main.IndexOf("<form", StringComparison.Ordinal);

    [Fact] // 005 AK-7
    public async Task Field_errors_mark_each_affected_field_and_stand_at_it_not_in_a_box()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        var form = SmtpForm(smtp, smtp.Password);
        form["Input.Host"] = "mx.mailhost.example:993";
        form["Input.Port"] = "993";

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", form);
        var main = MainContent(html);

        AssertErrorAtField(main, "Input.Host");
        AssertErrorAtField(main, "Input.Port");
        foreach (var name in new[] { "Input.Security", "Input.Username", "Input.Password", "Input.FromAddress", "Input.FromName", "TestRecipient" })
        {
            AssertNoErrorAtField(main, name);
        }

        Assert.Empty(Alerts(main, "alert-danger"));
        Assert.Empty(Alerts(main, "alert-success"));
    }

    [Fact] // 005 AK-7
    public async Task Error_without_field_stands_in_a_box_above_the_form()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", SmtpForm(smtp, "falsch"));
        var main = MainContent(html);

        var box = Assert.Single(Alerts(main, "alert-danger"));
        Assert.Contains("role=\"alert\"", box.StartTag, StringComparison.Ordinal);
        Assert.Contains("data-testid=\"errors\"", box.StartTag, StringComparison.Ordinal);
        Assert.Contains(MailErrors.ResourceManager.GetString(nameof(MailErrors.AuthenticationFailed), CultureInfo.InvariantCulture)!, box.Text, StringComparison.Ordinal);
        Assert.True(box.Index < FormStart(main));
        Assert.DoesNotContain("is-invalid", main, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-7
    public async Task Box_holds_only_errors_without_a_field()
    {
        await using var factory = new AdminFactory();
        using var client = factory.CreateFormClient();

        var (_, html) = await AdminTestFlows.LoginPasswordAsync(client, "", "");
        var main = MainContent(html);

        AssertErrorAtField(main, "Email");
        AssertErrorAtField(main, "Password");
        var box = Assert.Single(Alerts(main, "alert-danger"));
        Assert.NotEmpty(box.Text);
        Assert.True(box.Index < FormStart(main));
        Assert.DoesNotContain(Feedback(main, "Email").Text, box.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Feedback(main, "Password").Text, box.Text, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-7
    public async Task Invalid_invite_email_is_marked_at_the_field()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "kein-mail" });
        var main = MainContent(html);

        AssertErrorAtField(main, "InviteEmail");
        Assert.Equal(AdminTexts.ResourceManager.GetString("Validation.EmailInvalid", CultureInfo.InvariantCulture), Feedback(main, "InviteEmail").Text);
        Assert.Empty(Alerts(main, "alert-danger"));
    }

    [Fact] // 005 AK-7
    public async Task Invalid_test_recipient_is_marked_at_the_field()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);
        var form = SmtpForm(smtp, smtp.Password);
        form["TestRecipient"] = "kein-mail";

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", form);
        var main = MainContent(html);

        AssertErrorAtField(main, "TestRecipient");
        Assert.Empty(Alerts(main, "alert-danger"));
    }

    [Fact] // 005 AK-7
    public async Task Empty_second_factor_code_is_marked_at_the_field()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        using var client = factory.CreateFormClient();
        await AdminTestFlows.LoginPasswordAsync(client, admin.Email);

        var (_, html) = await AdminTestFlows.LoginCodeAsync(client, "");
        var main = MainContent(html);

        AssertErrorAtField(main, "Code");
        var expected = AdminTexts.ResourceManager.GetString("Validation.CodeRequired", CultureInfo.InvariantCulture);
        Assert.Equal(expected, Feedback(main, "Code").Text);
        // Die Seite meldet zusätzlich einen allgemeinen Fehler im Kasten; der Feldfehler steht dort nicht doppelt.
        Assert.DoesNotContain(expected!, Assert.Single(Alerts(main, "alert-danger")).Text, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-7
    public async Task Pages_without_errors_show_no_error_box_and_no_marked_field()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, get) = await admin.Client.GetAsync("/Smtp");
        var (_, saved) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", SmtpForm(smtp, smtp.Password));
        using var anonymous = factory.CreateFormClient();
        var (_, login) = await anonymous.GetAsync("/Login");

        foreach (var html in new[] { get, saved, login })
        {
            Assert.DoesNotContain("alert-danger", html, StringComparison.Ordinal);
            Assert.DoesNotContain("is-invalid", html, StringComparison.Ordinal);
        }
    }

    [Fact] // 005 AK-8
    public async Task Success_is_shown_as_success_message_above_the_form()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", SmtpForm(smtp, smtp.Password));
        var main = MainContent(html);

        var message = Assert.Single(Alerts(main, "alert-success"));
        Assert.Contains("data-testid=\"message\"", message.StartTag, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", message.StartTag, StringComparison.Ordinal);
        Assert.NotEmpty(message.Text);
        Assert.True(message.Index < FormStart(main));
        Assert.Empty(Alerts(main, "alert-danger"));
        Assert.Empty(Alerts(main, "alert-warning"));
    }

    [Fact] // 005 AK-8
    public async Task Success_message_on_the_admins_page()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "" });

        var message = Assert.Single(Alerts(MainContent(html), "alert-success"));
        Assert.Contains("data-testid=\"message\"", message.StartTag, StringComparison.Ordinal);
        Assert.NotEmpty(message.Text);
    }

    [Fact] // 005 AK-8
    public async Task Hint_is_shown_as_warning_message_until_settings_are_saved()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await SignedInAsync(factory);

        var (_, before) = await admin.Client.GetAsync("/Smtp");
        var hint = Assert.Single(Alerts(MainContent(before), "alert-warning"));
        Assert.Contains("data-testid=\"message\"", hint.StartTag, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", hint.StartTag, StringComparison.Ordinal);
        Assert.NotEmpty(hint.Text);
        Assert.Empty(Alerts(MainContent(before), "alert-success"));

        var (_, after) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", SmtpForm(smtp, smtp.Password));
        Assert.Empty(Alerts(MainContent(after), "alert-warning"));
    }

    [GeneratedRegex("\\bclass=\"([^\"]*)\"")]
    private static partial Regex ClassAttribute();

    [GeneratedRegex("<(?:input(?![^>]*type=\"hidden\")|select|textarea)\\b")]
    private static partial Regex FieldStart();

    [GeneratedRegex("<div\\b[^>]*\\bclass=\"(?:[^\"]*\\s)?alert(?:\\s[^\"]*)?\"[^>]*>(.*?)</div>", RegexOptions.Singleline)]
    private static partial Regex AlertDiv();

    [GeneratedRegex("</(?:label|div)>")]
    private static partial Regex ClosingContainer();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex AnyTag();
}
