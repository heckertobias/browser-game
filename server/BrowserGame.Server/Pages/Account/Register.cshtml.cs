using System.ComponentModel.DataAnnotations;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>
/// Registrierung mit E-Mail, Name und Passwort (003 AK-2, AK-5 bis AK-7b). Eine schon vergebene E-Mail-Adresse
/// verrät die Seite nicht: Sie zeigt dieselbe Erfolgsansicht, der Inhaber bekommt eine Mail.
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class RegisterModel(UserManager<PlayerAccount> users, AccountEmails emails) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Bitte E-Mail angeben.")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Bitte einen Spielernamen angeben.")]
    public string Name { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Bitte Passwort angeben.")]
    public string Password { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Bitte das Passwort wiederholen.")]
    public string ConfirmPassword { get; set; } = "";

    public bool Sent { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        var name = "";
        if (!string.IsNullOrWhiteSpace(Name))
        {
            var nameError = PlayerNames.TryNormalize(Name, out name);
            if (nameError != PlayerNameError.None)
            {
                ModelState.AddModelError(nameof(Name), PlayerNames.Describe(nameError));
            }
        }

        if (!string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
        {
            ModelState.AddModelError(nameof(ConfirmPassword), "Die Passwörter stimmen nicht überein.");
        }

        return ModelState.IsValid ? await CreateAsync(name) : Page();
    }

    private async Task<IActionResult> CreateAsync(string name)
    {
        var email = Email.Trim();
        var account = new PlayerAccount { UserName = name, Email = email };
        var result = await users.CreateAsync(account, Password);
        if (result.Succeeded)
        {
            await emails.SendConfirmationAsync(account);
            Sent = true;
            return Page();
        }

        var errors = result.Errors.Where(e => e.Code != nameof(IdentityErrorDescriber.DuplicateEmail)).ToList();
        if (errors.Count == 0)
        {
            if (await users.FindByEmailAsync(email) is { } existing)
            {
                await emails.SendAlreadyRegisteredAsync(existing);
            }

            Sent = true;
            return Page();
        }

        foreach (var error in errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return Page();
    }
}
