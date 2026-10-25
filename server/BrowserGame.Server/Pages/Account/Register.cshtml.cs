using System.ComponentModel.DataAnnotations;
using System.Globalization;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>
/// Registrierung mit E-Mail, Name und Passwort (003 AK-2, AK-5 bis AK-7b, 006 AK-7). Eine schon vergebene E-Mail-Adresse
/// verrät die Seite nicht: Sie zeigt dieselbe Erfolgsansicht, der Inhaber bekommt eine Mail.
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class RegisterModel(UserManager<PlayerAccount> users, AccountEmails emails, IStringLocalizer<Texts> t) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.NameRequired")]
    public string Name { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.PasswordRequired")]
    public string Password { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.ConfirmPasswordRequired")]
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
            ModelState.AddModelError(nameof(ConfirmPassword), t["Register.PasswordMismatch"]);
        }

        return ModelState.IsValid ? await CreateAsync(name) : Page();
    }

    private async Task<IActionResult> CreateAsync(string name)
    {
        var email = Email.Trim();
        var account = new PlayerAccount { UserName = name, Email = email, Language = GameLanguages.Normalize(CultureInfo.CurrentUICulture.Name) };
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
