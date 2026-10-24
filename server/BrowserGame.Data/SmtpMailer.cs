using System.Net.Sockets;
using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace BrowserGame.Data;

public enum MailStatus
{
    Sent,
    NotConfigured,
    Failed,
}

/// <summary>Ergebnis eines Versands; <see cref="Error"/> ist eine verständliche deutsche Meldung (004 AK-9).</summary>
public sealed record MailResult(MailStatus Status, string? Error = null)
{
    public static readonly MailResult Sent = new(MailStatus.Sent);
    public static readonly MailResult NotConfigured = new(MailStatus.NotConfigured, "Der Mailversand ist noch nicht eingerichtet.");
}

/// <summary>
/// Verschickt Mails mit den SMTP-Einstellungen aus der DB. Liest sie bei jedem Versand neu, damit Änderungen im
/// Admin-Interface sofort wirken (004 AK-10). Wirft nicht, sondern liefert ein <see cref="MailResult"/>.
/// </summary>
public sealed class SmtpMailer(IServiceScopeFactory scopes)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    public async Task<MailResult> SendAsync(string to, string subject, string text, string? html = null, CancellationToken cancellationToken = default)
    {
        SmtpSettings? settings;
        await using (var scope = scopes.CreateAsyncScope())
        {
            settings = await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().GetAsync(cancellationToken);
        }

        return settings is null ? MailResult.NotConfigured : await SendAsync(settings, to, subject, text, html, cancellationToken);
    }

    public static async Task<MailResult> SendAsync(
        SmtpSettings settings, string to, string subject, string text, string? html = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(settings.FromName ?? "", settings.FromAddress));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;
            message.Body = new BodyBuilder { TextBody = text, HtmlBody = html }.ToMessageBody();

            using var client = new SmtpClient { Timeout = (int)Timeout.TotalMilliseconds };
            await client.ConnectAsync(settings.Host, settings.Port, ToMailKit(settings.Security), cancellationToken);
            if (!string.IsNullOrEmpty(settings.Username))
            {
                await client.AuthenticateAsync(settings.Username, settings.Password ?? "", cancellationToken);
            }

            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(quit: true, cancellationToken);
            return MailResult.Sent;
        }
        catch (Exception ex) when (Describe(ex) is { } error)
        {
            return new MailResult(MailStatus.Failed, error);
        }
    }

    private static SecureSocketOptions ToMailKit(SmtpSecurity security) => security switch
    {
        SmtpSecurity.SslOnConnect => SecureSocketOptions.SslOnConnect,
        SmtpSecurity.StartTls => SecureSocketOptions.StartTls,
        SmtpSecurity.None => SecureSocketOptions.None,
        _ => SecureSocketOptions.Auto,
    };

    /// <summary>Übersetzt die typischen Fehler; <c>null</c> = unerwartet, wird weitergeworfen.</summary>
    internal static string? Describe(Exception exception) => exception switch
    {
        AuthenticationException => "Anmeldung am Mailserver fehlgeschlagen – Benutzername und Passwort prüfen.",
        SslHandshakeException => "Verschlüsselte Verbindung zum Mailserver fehlgeschlagen – Verschlüsselung und Port prüfen.",
        SocketException => "Mailserver nicht erreichbar – Server und Port prüfen.",
        SmtpCommandException command => $"Der Mailserver hat die Mail abgelehnt: {command.Message}",
        SmtpProtocolException or ServiceNotConnectedException => "Unerwartete Antwort vom Mailserver – Server, Port und Verschlüsselung prüfen.",
        ParseException or FormatException => "Ungültige Absender- oder Empfängeradresse.",
        TimeoutException or OperationCanceledException => "Zeitüberschreitung beim Mailserver.",
        IOException => "Verbindung zum Mailserver abgebrochen – Server, Port und Verschlüsselung prüfen.",
        _ => null,
    };
}
