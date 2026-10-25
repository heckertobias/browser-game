using System.Globalization;
using System.Text.Encodings.Web;
using BrowserGame.Data;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Accounts;

/// <summary>
/// Mailtexte in der Sprache des Accounts (006 AK-9), nicht der aktuellen Anfrage; der Versandweg ist austauschbar.
/// </summary>
public abstract class GameEmailSender : IEmailSender<Account>
{
    public Task SendConfirmationLinkAsync(Account user, string email, string confirmationLink) =>
        Compose(user, email, "Mail.Confirmation", confirmationLink, confirmationLink);

    public Task SendPasswordResetLinkAsync(Account user, string email, string resetLink) =>
        Compose(user, email, "Mail.PasswordReset", resetLink, resetLink);

    /// <summary>Registrierung mit einer schon vorhandenen Adresse: Hinweis an den Inhaber statt Fehlermeldung auf der Seite.</summary>
    public Task SendAlreadyRegisteredAsync(Account user, string email, string loginLink, string forgotPasswordLink) =>
        Compose(user, email, "Mail.AlreadyRegistered", loginLink, loginLink, forgotPasswordLink);

    /// <summary>Codes statt Links nutzen wir nicht (nur für das Identity-Interface).</summary>
    public Task SendPasswordResetCodeAsync(Account user, string email, string resetCode) =>
        throw new NotSupportedException("Passwort-Reset läuft über Links.");

    /// <summary>Text-Mail plus schlichtes HTML mit klickbarem Link; <paramref name="language"/> ist die Sprache des Accounts.</summary>
    protected abstract Task SendAsync(string recipient, string subject, string text, string link, string language);

    protected static string ToHtml(string text, string link, string language) =>
        $"<div lang=\"{HtmlEncoder.Default.Encode(language)}\">{MailHtml.FromText(text, link)}</div>";

    private Task Compose(Account user, string email, string key, string htmlLink, params string[] links)
    {
        var language = GameLanguages.Normalize(user.Language);
        var culture = CultureInfo.GetCultureInfo(language);
        var subject = Text(key + ".Subject", culture);
        var text = Format(culture, Text(key + ".Body", culture), [user.UserName, .. links]);
        return SendAsync(email, subject, text, htmlLink, language);
    }

    private static string Text(string key, CultureInfo culture) => Texts.ResourceManager.GetString(key, culture) ?? key;

#pragma warning disable CA1863 // Vorlage kommt je Sprache aus der Ressource; Zwischenspeichern lohnt nicht.
    private static string Format(CultureInfo culture, string format, params object?[] args) => string.Format(culture, format, args);
#pragma warning restore CA1863
}

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
