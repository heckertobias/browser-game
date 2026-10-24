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

        var (_, tested) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", new Dictionary<string, string> { ["TestRecipient"] = "t@example.org" });
        Assert.Contains("Testmail an t@example.org verschickt.", tested, StringComparison.Ordinal);
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
        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", new Dictionary<string, string> { ["TestRecipient"] = "t@example.org" });

        Assert.Single(smtp.Messages); // Anmeldung am Server hat mit dem alten Passwort geklappt
    }

    [Fact] // AK-9: verständliche Fehlermeldung
    public async Task Wrong_password_shows_german_error()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Save", Form(smtp, "falsch"));
        var (_, html) = await admin.Client.PostAsync("/Smtp", "/Smtp?handler=Test", new Dictionary<string, string> { ["TestRecipient"] = "t@example.org" });

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
}
