using System.Net;
using System.Net.Sockets;
using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

public sealed class SmtpMailerTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static SmtpSettings For(FakeSmtpServer server, string? password = null) =>
        new("127.0.0.1", server.Port, SmtpSecurity.None, server.Username, password ?? server.Password, "noreply@example.net", "Browser Game");

    [Fact] // AK-9: Testmail kommt an
    public async Task Mail_is_delivered()
    {
        await using var server = new FakeSmtpServer();

        var result = await SmtpMailer.SendAsync(For(server), "t@example.org", "Testmail", "Hallo", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(MailResult.Sent, result);
        var message = Assert.Single(server.Messages);
        Assert.Contains("Subject: Testmail", message, StringComparison.Ordinal);
        Assert.Contains("From: Browser Game <noreply@example.net>", message, StringComparison.Ordinal);
    }

    [Fact] // AK-9: verständliche Meldung bei falschen Zugangsdaten
    public async Task Wrong_password_gives_german_error()
    {
        await using var server = new FakeSmtpServer();

        var result = await SmtpMailer.SendAsync(For(server, password: "falsch"), "t@example.org", "Testmail", "Hallo", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(MailStatus.Failed, result.Status);
        Assert.Equal("Anmeldung am Mailserver fehlgeschlagen – Benutzername und Passwort prüfen.", result.Error);
        Assert.Empty(server.Messages);
    }

    [Fact] // AK-9: verständliche Meldung, wenn der Server nicht erreichbar ist
    public async Task Unreachable_server_gives_german_error()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop(); // Port ist jetzt frei → Verbindung wird abgelehnt

        var settings = new SmtpSettings("127.0.0.1", port, SmtpSecurity.None, null, null, "noreply@example.net", null);
        var result = await SmtpMailer.SendAsync(settings, "t@example.org", "Testmail", "Hallo", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("Mailserver nicht erreichbar – Server und Port prüfen.", result.Error);
    }

    [Fact] // AK-11: ohne Einstellungen wird nicht verschickt, aber auch nicht geworfen
    public async Task Without_settings_mail_is_not_configured()
    {
        await using var fresh = new GameServerFactory();
        var mailer = fresh.Services.GetRequiredService<SmtpMailer>();

        var result = await mailer.SendAsync("t@example.org", "Test", "Hallo", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(MailResult.NotConfigured, result);
    }

    [Fact] // AK-10: geänderte Einstellungen gelten ab der nächsten Mail, ohne Neustart
    public async Task Changed_settings_apply_to_the_next_mail()
    {
        await using var first = new FakeSmtpServer();
        await using var second = new FakeSmtpServer();
        var mailer = factory.Services.GetRequiredService<SmtpMailer>();
        var ct = TestContext.Current.CancellationToken;

        await SaveAsync(For(first));
        await mailer.SendAsync("t@example.org", "Eins", "Hallo", cancellationToken: ct);
        await SaveAsync(For(second));
        await mailer.SendAsync("t@example.org", "Zwei", "Hallo", cancellationToken: ct);

        Assert.Contains("Subject: Eins", Assert.Single(first.Messages), StringComparison.Ordinal);
        Assert.Contains("Subject: Zwei", Assert.Single(second.Messages), StringComparison.Ordinal);
    }

    private async Task SaveAsync(SmtpSettings settings)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().SaveAsync(settings, TestContext.Current.CancellationToken);
    }
}
