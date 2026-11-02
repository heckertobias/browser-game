using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Pages;

/// <summary>Anmeldung per Passkey oder Passwort (danach Pflicht-2FA) – 004 AK-2, AK-4, AK-4b.</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginModel(SignInManager<AdminUser> signIn, UserManager<AdminUser> users, IStringLocalizer<AdminTexts> t) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.PasswordRequired")]
    public string Password { get; set; } = "";

    [BindProperty]
    public string? Credential { get; set; }

    /// <summary>WebAuthn-Optionen für „Mit Passkey anmelden“ (ohne vorher bekannten Nutzer).</summary>
    public string PasskeyOptions { get; private set; } = "{}";

    public async Task OnGetAsync() => PasskeyOptions = await signIn.MakePasskeyRequestOptionsAsync(null);

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        ModelState.Remove(nameof(Credential));
        if (ModelState.IsValid && await users.FindByEmailAsync(Email) is { } user)
        {
            // Passwort ohne zweiten Faktor ist für Admins nicht erlaubt (unvollständige Einrichtung).
            if (user.TwoFactorEnabled)
            {
                var result = await signIn.PasswordSignInAsync(user, Password, isPersistent: false, lockoutOnFailure: true);
                if (result.RequiresTwoFactor)
                {
                    return RedirectToPage("LoginTwoFactor");
                }
            }
        }

        ModelState.AddModelError("", t["Login.Failed"]);
        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        if (!string.IsNullOrEmpty(Credential))
        {
            var result = await signIn.PasskeySignInAsync(Credential);
            if (result.Succeeded)
            {
                return LocalRedirect("/");
            }
        }

        ModelState.Clear();
        ModelState.AddModelError("", t["Login.PasskeyFailed"]);
        await OnGetAsync();
        return Page();
    }
}
