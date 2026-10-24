using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Accounts;

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

    protected static string ToHtml(string text, string link) => MailHtml.FromText(text, link);
}

/// <summary>
/// Versand über den gemeinsamen <see cref="SmtpMailer"/> (Einstellungen aus dem Admin-Interface, 004).
/// Fehlen die Einstellungen oder scheitert der Versand, läuft die Registrierung weiter (004 AK-11); Spieler können die
/// Mail erneut anfordern. Links landen nur in der Entwicklung im Log – sie sind Geheimnisse.
/// </summary>
internal sealed partial class PlayerEmailSender(SmtpMailer mailer, IWebHostEnvironment environment, ILogger<PlayerEmailSender> logger) : GameEmailSender
{
    protected override async Task SendAsync(string to, string subject, string text, string link)
    {
        var result = await mailer.SendAsync(to, subject, text, ToHtml(text, link));
        if (result.Status == MailStatus.Sent)
        {
            return;
        }

        if (environment.IsDevelopment())
        {
            LogNotSentWithLink(subject, result.Error, link);
        }
        else
        {
            LogNotSent(subject, result.Error);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail „{Subject}“ nicht verschickt: {Error}")]
    private partial void LogNotSent(string subject, string? error);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Mail „{Subject}“ nicht verschickt: {Error} – Link (nur Entwicklung): {Link}")]
    private partial void LogNotSentWithLink(string subject, string? error, string link);
}

internal static class EmailSetup
{
    public static void AddGameEmail(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<GameEmailSender, PlayerEmailSender>();
        builder.Services.AddSingleton<IEmailSender<Account>>(sp => sp.GetRequiredService<GameEmailSender>());
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<AccountLinks>();
        builder.Services.AddScoped<AccountEmails>();
    }
}
