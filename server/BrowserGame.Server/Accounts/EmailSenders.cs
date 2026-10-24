using System.Text.Encodings.Web;
using BrowserGame.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.AspNetCore.Identity;
using MimeKit;

namespace BrowserGame.Server.Accounts;

/// <summary>SMTP-Zugang aus der Konfiguration (<c>Mail:*</c>, im Deployment aus der .env).</summary>
internal sealed class MailOptions
{
    public string? Host { get; set; }
    public int Port { get; set; } = 465;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string From { get; set; } = "";
}

/// <summary>Deutsche Mailtexte für Identity (Bestätigen, Passwort zurücksetzen); der Versandweg ist austauschbar.</summary>
internal abstract class GameEmailSender : IEmailSender<Account>
{
    public Task SendConfirmationLinkAsync(Account user, string email, string confirmationLink) =>
        SendAsync(email, "Bitte bestätige deine E-Mail-Adresse",
            $"Hallo {user.UserName},\n\nbitte bestätige deine E-Mail-Adresse, damit du spielen kannst:\n{confirmationLink}\n\n"
            + "Der Link ist 3 Stunden gültig. Wenn du dich nicht registriert hast, kannst du diese Mail ignorieren.",
            confirmationLink);

    public Task SendPasswordResetLinkAsync(Account user, string email, string resetLink) =>
        SendAsync(email, "Passwort zurücksetzen",
            $"Hallo {user.UserName},\n\nüber diesen Link kannst du ein neues Passwort setzen:\n{resetLink}\n\n"
            + "Der Link ist 3 Stunden gültig und funktioniert nur einmal. Wenn du das nicht angefordert hast, ignoriere diese Mail.",
            resetLink);

    /// <summary>Registrierung mit einer schon vorhandenen Adresse: Hinweis an den Inhaber statt Fehlermeldung auf der Seite.</summary>
    public Task SendAlreadyRegisteredAsync(Account user, string email, string loginLink, string forgotPasswordLink) =>
        SendAsync(email, "Du hast schon einen Account",
            $"Hallo {user.UserName},\n\njemand hat versucht, mit deiner E-Mail-Adresse einen neuen Account anzulegen. "
            + $"Du hast bereits einen. Hier kannst du dich anmelden:\n{loginLink}\n\n"
            + $"Passwort vergessen? {forgotPasswordLink}\n\nWenn du das nicht warst, musst du nichts tun.",
            loginLink);

    /// <summary>Codes statt Links nutzen wir nicht (nur für das Identity-Interface).</summary>
    public Task SendPasswordResetCodeAsync(Account user, string email, string resetCode) =>
        throw new NotSupportedException("Passwort-Reset läuft über Links.");

    /// <summary>Text-Mail plus schlichtes HTML mit klickbarem Link.</summary>
    protected abstract Task SendAsync(string to, string subject, string text, string link);

    protected static string ToHtml(string text, string link)
    {
        var encoded = HtmlEncoder.Default.Encode(text).Replace("&#xA;", "<br>", StringComparison.Ordinal);
        var encodedLink = HtmlEncoder.Default.Encode(link);
        return encoded.Replace(encodedLink, $"<a href=\"{encodedLink}\">{encodedLink}</a>", StringComparison.Ordinal);
    }
}

internal sealed class SmtpEmailSender(MailOptions options) : GameEmailSender
{
    protected override async Task SendAsync(string to, string subject, string text, string link)
    {
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse(options.From));
        message.To.Add(MailboxAddress.Parse(to));
        message.Subject = subject;
        message.Body = new BodyBuilder { TextBody = text, HtmlBody = ToHtml(text, link) }.ToMessageBody();

        using var client = new SmtpClient();
        await client.ConnectAsync(options.Host ?? throw new InvalidOperationException("Mail:Host fehlt."), options.Port, SecureSocketOptions.Auto);
        if (!string.IsNullOrEmpty(options.Username))
        {
            await client.AuthenticateAsync(options.Username, options.Password ?? "");
        }

        await client.SendAsync(message);
        await client.DisconnectAsync(quit: true);
    }
}

/// <summary>Entwicklung: Mails ins Log schreiben statt verschicken.</summary>
internal sealed partial class LogEmailSender(ILogger<LogEmailSender> logger) : GameEmailSender
{
    protected override Task SendAsync(string to, string subject, string text, string link)
    {
        LogMail(to, subject, link);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail (nicht verschickt) an {To}: {Subject} – Link: {Link}")]
    private partial void LogMail(string to, string subject, string link);
}

internal static class EmailSetup
{
    public static void AddGameEmail(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration.GetSection("Mail").Get<MailOptions>() ?? new MailOptions();
        if (!string.IsNullOrWhiteSpace(options.Host))
        {
            if (string.IsNullOrWhiteSpace(options.From))
            {
                throw new InvalidOperationException("Mail:From fehlt.");
            }

            builder.Services.AddSingleton(options);
            builder.Services.AddSingleton<GameEmailSender, SmtpEmailSender>();
        }
        else if (builder.Environment.IsDevelopment())
        {
            builder.Services.AddSingleton<GameEmailSender, LogEmailSender>();
        }
        else
        {
            // Ohne Mailversand könnte sich niemand registrieren – lieber gar nicht starten als still scheitern.
            throw new InvalidOperationException("Mail:Host fehlt (SMTP für Bestätigungs- und Reset-Mails).");
        }

        builder.Services.AddSingleton<IEmailSender<Account>>(sp => sp.GetRequiredService<GameEmailSender>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AccountLinks>();
        builder.Services.AddScoped<AccountEmails>();
    }
}
