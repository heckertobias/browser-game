using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace BrowserGame.Admin.Pages;

/// <summary>Anmeldung per Passkey oder Passwort (danach Pflicht-2FA) – 004 AK-2, AK-4, AK-4b.</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginModel(SignInManager<AdminUser> signIn, UserManager<AdminUser> users) : PageModel
{
    internal const string GenericError = "Anmeldung fehlgeschlagen. Nach mehreren Fehlversuchen ist die Anmeldung kurz gesperrt.";

    [BindProperty]
    [Required(ErrorMessage = "Bitte E-Mail angeben.")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Bitte Passwort angeben.")]
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

        ModelState.AddModelError("", GenericError);
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
        ModelState.AddModelError("", "Anmeldung mit Passkey fehlgeschlagen.");
        await OnGetAsync();
        return Page();
    }
}
