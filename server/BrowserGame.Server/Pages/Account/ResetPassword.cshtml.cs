using System.ComponentModel.DataAnnotations;
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
/// Neues Passwort über den Link aus der Mail (003 AK-15). Der Link gilt 3 Stunden und nur einmal: Das Zurücksetzen ändert
/// den Security-Stamp, damit werden der Link und alle anderen Sitzungen ungültig (spätestens nach 1 min, AccountSetup).
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class ResetPasswordModel(UserManager<PlayerAccount> users, IStringLocalizer<Texts> t) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? UserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Code { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Validation.NewPasswordRequired")]
    public string Password { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.ConfirmPasswordRequired")]
    public string ConfirmPassword { get; set; } = "";

    public bool Invalid { get; private set; }

    public bool Done { get; private set; }

    public async Task OnGetAsync()
    {
        Invalid = await FindValidAsync() is null;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await FindValidAsync() is not { } found)
        {
            Invalid = true;
            return Page();
        }

        if (!string.IsNullOrEmpty(ConfirmPassword) && Password != ConfirmPassword)
        {
            ModelState.AddModelError(nameof(ConfirmPassword), t["Register.PasswordMismatch"]);
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var result = await users.ResetPasswordAsync(found.User, found.Token, Password);
        if (result.Succeeded)
        {
            await users.SetLockoutEndDateAsync(found.User, null);
            await users.ResetAccessFailedCountAsync(found.User);
            // 003 T11: hier GameLoop.ForceLeave – laufende Spiel-Verbindung sofort beenden (AK-15).
            Done = true;
            return Page();
        }

        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.InvalidToken)))
        {
            Invalid = true;
            return Page();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return Page();
    }

    private async Task<(PlayerAccount User, string Token)?> FindValidAsync()
    {
        if (string.IsNullOrEmpty(UserId) || AccountLinks.DecodeToken(Code) is not { } token || await users.FindByIdAsync(UserId) is not { } user)
        {
            return null;
        }

        var valid = await users.VerifyUserTokenAsync(
            user, users.Options.Tokens.PasswordResetTokenProvider, UserManager<PlayerAccount>.ResetPasswordTokenPurpose, token);
        return valid ? (user, token) : null;
    }
}
