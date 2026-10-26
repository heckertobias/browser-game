using System.Net;
using BrowserGame.Data;
using BrowserGame.Server.Tests;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Tests;

public sealed class SmtpPageTests
{
    private static Dictionary<string, string> Form(FakeSmtpServer server, string? password) => new()
    {
        ["Input.Host"] = "127.0.0.1",
        ["Input.Port"] = server.Port.ToString(System.Globalization.CultureInfo.InvariantCulture),
        ["Input.Security"] = nameof(SmtpSecurity.None),
        ["Input.Username"] = server.Username,
        ["Input.Password"] = password ?? "",
        ["Input.FromAddress"] = "noreply@example.net",
        ["Input.FromName"] = "Browser Game",
    };

    private static Dictionary<string, string> WithRecipient(Dictionary<string, string> form, string recipient = "t@example.org")
    {
        form["TestRecipient"] = recipient;
        return form;
    }

    [Fact] // AK-7, AK-8, AK-9, AK-12
    public async Task Settings_are_saved_password_hidden_test_mail_sent_and_audited()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, saved) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", Form(smtp, smtp.Password));
        Assert.Contains("Gespeichert.", saved, StringComparison.Ordinal);
        Assert.DoesNotContain(smtp.Password, saved, StringComparison.Ordinal); // AK-8: nie wieder angezeigt
        Assert.Contains("gesetzt", WebUtility.HtmlDecode(saved), StringComparison.Ordinal);

        var (_, page) = await admin.Client.GetAsync("/Smtp");
        Assert.DoesNotContain(smtp.Password, page, StringComparison.Ordinal);

        var (_, tested) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", WithRecipient(Form(smtp, password: null)));
        Assert.Contains("Testmail an t@example.org verschickt", tested, StringComparison.Ordinal);
        Assert.Contains("Subject: Testmail", Assert.Single(smtp.Messages), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var entries = await scope.ServiceProvider.GetRequiredService<AdminDbContext>().AuditEntries.Select(e => e.Action + "|" + e.Details).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Contains(entries, e => e.StartsWith("SMTP-Einstellungen geändert|Felder: Host", StringComparison.Ordinal));
        Assert.Contains(entries, e => e.StartsWith("Testmail gesendet|an t@example.org", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Contains(smtp.Password, StringComparison.Ordinal));
    }

    [Fact] // AK-8: leeres Passwortfeld lässt das gespeicherte Passwort unverändert
    public async Task Empty_password_field_keeps_stored_password()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", Form(smtp, smtp.Password));
        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", Form(smtp, password: null));
        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", WithRecipient(Form(smtp, password: null)));

        Assert.Single(smtp.Messages); // Anmeldung am Server hat mit dem alten Passwort geklappt
    }

    [Fact] // AK-9: verständliche Fehlermeldung
    public async Task Wrong_password_shows_german_error()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", WithRecipient(Form(smtp, "falsch")));

        Assert.Contains("Anmeldung am Mailserver fehlgeschlagen", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-3
    public async Task Smtp_page_requires_login()
    {
        await using var factory = new AdminFactory();
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/Smtp");

        Assert.Equal("/Login", response.Headers.Location?.AbsolutePath);
    }

    [Fact] // Testmail mit den Formularwerten, ohne zu speichern; Eingaben bleiben stehen
    public async Task Test_mail_uses_form_values_without_saving_and_keeps_input()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", WithRecipient(Form(smtp, smtp.Password)));

        Assert.Contains("Testmail an t@example.org verschickt", html, StringComparison.Ordinal);
        Assert.Single(smtp.Messages);
        Assert.Contains("value=\"noreply@example.net\"", html, StringComparison.Ordinal); // Eingabe noch da
        Assert.DoesNotContain(smtp.Password, html, StringComparison.Ordinal);               // Passwort nie zurück
        await using var scope = factory.Services.CreateAsyncScope();
        Assert.Null(await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().GetAsync(TestContext.Current.CancellationToken));
    }

    [Theory] // Typische Verwechslungen werden verständlich abgefangen
    [InlineData("mx.mailhost.example:993", 465, "ohne Port")]
    [InlineData("https://mx.mailhost.example", 465, "ohne Port, ohne https://")]
    [InlineData("mx.mailhost.example", 993, "Port 993 ist IMAP")]
    [InlineData("mx.mailhost.example", 995, "Port 995 ist POP3")]
    public async Task Common_mistakes_are_explained(string host, int port, string expected)
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        var form = Form(smtp, smtp.Password);
        form["Input.Host"] = host;
        form["Input.Port"] = port.ToString(System.Globalization.CultureInfo.InvariantCulture);

        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", form);

        Assert.Contains(expected, WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.DoesNotContain("Gespeichert.", html, StringComparison.Ordinal);
    }

    [Fact] // Empfänger ist mit der eigenen Admin-Adresse vorbelegt; ohne Fehler kein (leerer) Fehlerkasten
    public async Task Recipient_is_prefilled_and_no_empty_error_box()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.GetAsync("/Smtp");

        Assert.Contains("value=\"admin@example.org\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("form-control", html, StringComparison.Ordinal);
    }
}
