using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>Erzeugt Token und Link und verschickt die passende Mail (003 AK-2, AK-3, AK-14).</summary>
public sealed class AccountEmails(UserManager<Account> users, GameEmailSender mail, AccountLinks links, RegistrationLinks registrationLinks)
{
    /// <summary>Link zu Schritt 2 (003 AK-2); <paramref name="language"/> ist die Sprache der Anfrage.</summary>
    public Task SendRegistrationLinkAsync(string email, string language)
    {
        var minutes = registrationLinks.LifetimeMinutes;
        var code = registrationLinks.CreateCode(email, TimeSpan.FromMinutes(minutes));
        var link = links.Absolute($"/Account/CompleteRegistration?code={Uri.EscapeDataString(code)}");
        return mail.SendRegistrationLinkAsync(email, language, link, minutes);
    }

    /// <summary>Link zum Zurücksetzen des Passworts (003 AK-14), in der Sprache des Accounts (006 AK-9).</summary>
    public async Task SendPasswordResetAsync(Account account)
    {
        var token = AccountLinks.EncodeToken(await users.GeneratePasswordResetTokenAsync(account));
        var link = links.Absolute($"/Account/ResetPassword?userId={Uri.EscapeDataString(account.Id)}&code={token}");
        await mail.SendPasswordResetLinkAsync(account, account.Email!, link);
    }

    /// <summary>Hinweis an den Inhaber einer schon registrierten Adresse (003 AK-3), mit Links zu Anmeldung und Passwort-Reset.</summary>
    public Task SendAlreadyRegisteredAsync(Account account) =>
        mail.SendAlreadyRegisteredAsync(account, account.Email!, links.Absolute("/Account/Login"), links.Absolute("/Account/ForgotPassword"));
}
