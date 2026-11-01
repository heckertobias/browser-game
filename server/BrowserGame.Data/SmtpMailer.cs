using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
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

/// <summary>
/// Ergebnis eines Versands; <see cref="Error"/> ist eine verständliche Meldung in der Sprache der Anfrage, neutral Englisch (004 AK-9, 005 AK-14),
/// <see cref="Detail"/> die technische Ursache fürs Log.
/// </summary>
public sealed record MailResult(MailStatus Status, string? Error = null, string? Detail = null)
{
    public static readonly MailResult Sent = new(MailStatus.Sent);
    public static MailResult NotConfigured => new(MailStatus.NotConfigured, MailErrors.NotConfigured);
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
        catch (Exception ex) when (Describe(ex, settings.Host) is { } error)
        {
            return new MailResult(MailStatus.Failed, error, ex.InnerException is null ? ex.Message : $"{ex.Message} → {ex.InnerException.Message}");
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
    internal static string? Describe(Exception exception, string host) => exception switch
    {
        AuthenticationException => MailErrors.AuthenticationFailed,
        SslHandshakeException { ServerCertificate: { } certificate } when CertificateNames(certificate) is { Count: > 0 } names
            && !names.Any(n => MatchesHost(n, host))
            => MailErrors.CertificateNameMismatch(names.Take(3), host),
        SslHandshakeException => MailErrors.SslHandshakeFailed,
        SocketException => MailErrors.Unreachable,
        SmtpCommandException command => MailErrors.Rejected(command.Message),
        SmtpProtocolException or ServiceNotConnectedException => MailErrors.UnexpectedResponse,
        ParseException or FormatException => MailErrors.InvalidAddress,
        TimeoutException or OperationCanceledException => MailErrors.TimedOut,
        IOException => MailErrors.ConnectionLost,
        ArgumentException => MailErrors.InvalidSettings,
        _ => null,
    };

    /// <summary>DNS-Namen eines Zertifikats (Subject Alternative Names, sonst Common Name).</summary>
    internal static IReadOnlyList<string> CertificateNames(X509Certificate certificate)
    {
        using var cert = new X509Certificate2(certificate);
        var names = cert.Extensions.OfType<X509SubjectAlternativeNameExtension>().SelectMany(e => e.EnumerateDnsNames()).ToList();
        if (names.Count == 0 && cert.GetNameInfo(X509NameType.DnsName, forIssuer: false) is { Length: > 0 } commonName)
        {
            names.Add(commonName);
        }

        return names;
    }

    /// <summary>Hostname passt zum Zertifikatsnamen, auch mit einfachem Platzhalter (*.example.net).</summary>
    internal static bool MatchesHost(string certificateName, string host)
    {
        if (string.Equals(certificateName, host, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return certificateName.StartsWith("*.", StringComparison.Ordinal)
            && host.IndexOf('.', StringComparison.Ordinal) is var dot and > 0
            && string.Equals(certificateName[2..], host[(dot + 1)..], StringComparison.OrdinalIgnoreCase);
    }
}
