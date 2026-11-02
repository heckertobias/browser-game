using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Pages;

/// <summary>Zweiter Faktor nach dem Passwort: Code aus der Authenticator-App oder Wiederherstellungscode (004 AK-4b, AK-4c).</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginTwoFactorModel(SignInManager<AdminUser> signIn, IStringLocalizer<AdminTexts> t) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.CodeRequired")]
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

        // Leerer Code: nur der Feldfehler, kein allgemeiner Fehler oben (005 AK-7).
        if (!ModelState.IsValid)
        {
            return Page();
        }

        // Wiederherstellungscodes enthalten einen Bindestrich und werden exakt verglichen; nur TOTP-Codes säubern.
        var code = Code.Replace(" ", "", StringComparison.Ordinal);
        var result = IsRecoveryCode
            ? await signIn.TwoFactorRecoveryCodeSignInAsync(code)
            : await signIn.TwoFactorAuthenticatorSignInAsync(code.Replace("-", "", StringComparison.Ordinal), isPersistent: false, rememberClient: false);
        if (result.Succeeded)
        {
            return LocalRedirect("/");
        }

        ModelState.AddModelError("", t["LoginTwoFactor.CodeWrong"]);
        return Page();
    }
}
