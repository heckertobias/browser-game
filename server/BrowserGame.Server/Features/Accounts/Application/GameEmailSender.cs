using System.Globalization;
using System.Text.Encodings.Web;
using BrowserGame.Data;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Localization.Resources;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>
/// Mailtexte in der Sprache des Accounts (006 AK-9); der Registrierungslink vor dem Account in der Sprache der Anfrage.
/// Der Versandweg ist austauschbar.
/// </summary>
public abstract class GameEmailSender
{
    /// <summary>Link zu Schritt 2 der Registrierung (003 AK-2). Es gibt noch keinen Account: Sprache der Anfrage.</summary>
    public Task SendRegistrationLinkAsync(string email, string language, string link, int lifetimeMinutes) =>
        Compose(language, email, "Mail.Registration", link, link, lifetimeMinutes);

    public Task SendPasswordResetLinkAsync(Account user, string email, string resetLink) =>
        Compose(user.Language, email, "Mail.PasswordReset", resetLink, user.UserName, resetLink);

    /// <summary>Registrierung mit einer schon vorhandenen Adresse: Hinweis an den Inhaber statt Fehlermeldung auf der Seite.</summary>
    public Task SendAlreadyRegisteredAsync(Account user, string email, string loginLink, string forgotPasswordLink) =>
        Compose(user.Language, email, "Mail.AlreadyRegistered", loginLink, user.UserName, loginLink, forgotPasswordLink);

    /// <summary>Text-Mail plus schlichtes HTML mit klickbarem Link; <paramref name="language"/> ist die Sprache der Mail.</summary>
    protected abstract Task SendAsync(string recipient, string subject, string text, string link, string language);

    protected static string ToHtml(string text, string link, string language) =>
        $"<div lang=\"{HtmlEncoder.Default.Encode(language)}\">{MailHtml.FromText(text, link)}</div>";

    private Task Compose(string? language, string email, string key, string htmlLink, params object?[] args)
    {
        var normalized = GameLanguages.Normalize(language);
        var culture = CultureInfo.GetCultureInfo(normalized);
        var subject = Text(key + ".Subject", culture);
        var text = Format(culture, Text(key + ".Body", culture), args);
        return SendAsync(email, subject, text, htmlLink, normalized);
    }

    private static string Text(string key, CultureInfo culture) => Texts.ResourceManager.GetString(key, culture) ?? key;

#pragma warning disable CA1863 // Vorlage kommt je Sprache aus der Ressource; Zwischenspeichern lohnt nicht.
    private static string Format(CultureInfo culture, string format, params object?[] args) => string.Format(culture, format, args);
#pragma warning restore CA1863
}
