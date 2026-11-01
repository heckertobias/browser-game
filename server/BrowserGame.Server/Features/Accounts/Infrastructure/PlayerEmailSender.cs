using BrowserGame.Data;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>
/// Versand über den gemeinsamen <see cref="SmtpMailer"/> (Einstellungen aus dem Admin-Interface, 004).
/// Fehlen die Einstellungen oder scheitert der Versand, läuft die Registrierung weiter (004 AK-11); Spieler können die
/// Mail erneut anfordern. Links landen nur in der Entwicklung im Log – sie sind Geheimnisse.
/// </summary>
internal sealed partial class PlayerEmailSender(SmtpMailer mailer, IWebHostEnvironment environment, ILogger<PlayerEmailSender> logger) : GameEmailSender
{
    protected override async Task SendAsync(string recipient, string subject, string text, string link, string language)
    {
        var result = await mailer.SendAsync(recipient, subject, text, ToHtml(text, link, language));
        if (result.Status == MailStatus.Sent)
        {
            return;
        }

        if (environment.IsDevelopment())
        {
            LogNotSentWithLink(subject, result.Error, result.Detail, link);
        }
        else
        {
            LogNotSent(subject, result.Error, result.Detail);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail „{Subject}“ nicht verschickt: {Error} ({Detail})")]
    private partial void LogNotSent(string subject, string? error, string? detail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail „{Subject}“ nicht verschickt: {Error} ({Detail}) – Link (nur Entwicklung): {Link}")]
    private partial void LogNotSentWithLink(string subject, string? error, string? detail, string link);
}
