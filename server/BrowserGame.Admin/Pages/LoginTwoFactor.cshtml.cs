using System.ComponentModel.DataAnnotations;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace BrowserGame.Admin.Pages;

/// <summary>Zweiter Faktor nach dem Passwort: Code aus der Authenticator-App oder Wiederherstellungscode (004 AK-4b, AK-4c).</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginTwoFactorModel(SignInManager<AdminUser> signIn) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Bitte Code angeben.")]
    public string Code { get; set; } = "";

    [BindProperty]
    public bool IsRecoveryCode { get; set; }

    public async Task<IActionResult> OnGetAsync() =>
        await signIn.GetTwoFactorAuthenticationUserAsync() is null ? RedirectToPage("Login") : Page();

    public async Task<IActionResult> OnPostAsync()
    {
        if (await signIn.GetTwoFactorAuthenticationUserAsync() is null)
        {
            return RedirectToPage("Login");
        }

        if (ModelState.IsValid)
        {
            // Wiederherstellungscodes enthalten einen Bindestrich und werden exakt verglichen; nur TOTP-Codes säubern.
            var code = Code.Replace(" ", "", StringComparison.Ordinal);
            var result = IsRecoveryCode
                ? await signIn.TwoFactorRecoveryCodeSignInAsync(code)
                : await signIn.TwoFactorAuthenticatorSignInAsync(code.Replace("-", "", StringComparison.Ordinal), isPersistent: false, rememberClient: false);
            if (result.Succeeded)
            {
                return LocalRedirect("/");
            }
        }

        ModelState.AddModelError("", "Der Code stimmt nicht. Nach mehreren Fehlversuchen ist die Anmeldung kurz gesperrt.");
        return Page();
    }
}
