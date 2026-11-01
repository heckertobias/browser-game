using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BrowserGame.Data;
using BrowserGame.Server.Features.Accounts;
using BrowserGame.Server.Features.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Features.Accounts.Presentation.Account;

/// <summary>
/// Schritt 1 der Registrierung (003 AK-2, AK-3, AK-9): nur die E-Mail-Adresse. Die Antwort ist für jede Adresse gleich;
/// neue Adressen bekommen den Link zu Schritt 2, vorhandene einen Hinweis. In der DB entsteht nichts.
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class RegisterModel(UserManager<PlayerAccount> users, AccountEmails emails, RegistrationLinks registrationLinks) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public int LifetimeMinutes => registrationLinks.LifetimeMinutes;

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Email.Trim();
        if (!new EmailAddressAttribute().IsValid(email))
        {
            ModelState.AddModelError(nameof(Email), users.ErrorDescriber.InvalidEmail(email).Description);
            return Page();
        }

        if (await users.FindByEmailAsync(email) is { } existing)
        {
            await emails.SendAlreadyRegisteredAsync(existing);
        }
        else
        {
            await emails.SendRegistrationLinkAsync(email, GameLanguages.Normalize(CultureInfo.CurrentUICulture.Name));
        }

        Sent = true;
        return Page();
    }
}
