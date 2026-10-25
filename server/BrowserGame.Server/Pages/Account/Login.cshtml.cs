using System.ComponentModel.DataAnnotations;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>Anmeldung mit E-Mail und Passwort (003 AK-1, AK-9). Alle Fehlerfälle zeigen dieselbe Meldung.</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginModel(SignInManager<PlayerAccount> signIn, UserManager<PlayerAccount> users) : PageModel
{
    internal const string GenericError = "Anmeldung fehlgeschlagen. Prüfe E-Mail und Passwort. Nach mehreren Fehlversuchen ist die Anmeldung kurz gesperrt.";

    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.PasswordRequired")]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && await users.FindByEmailAsync(Email.Trim()) is { } user)
        {
            var result = await signIn.PasswordSignInAsync(user, Password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return LocalRedirect(AccountLinks.SafeReturnUrl(ReturnUrl, Url.IsLocalUrl));
            }
        }

        ModelState.Clear();
        ModelState.AddModelError("", GenericError);
        return Page();
    }
}
