using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>
/// Eigenen Account löschen, bestätigt mit Passwort oder Passkey (003 AK-16). Falsche Passwörter zählen zur Sperre (AK-10).
/// Position, Passkeys und Logins fallen per Cascade mit; der Name wird wieder frei.
/// </summary>
[Authorize]
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class DeleteModel(
    UserManager<PlayerAccount> users, SignInManager<PlayerAccount> signIn, PasskeyCeremonies ceremonies, IStringLocalizer<Texts> t) : PageModel
{
    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? Credential { get; set; }

    public bool HasPassword { get; private set; }

    public string? PasskeyOptions { get; private set; }

    public bool Deleted { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        return await ShowAsync(user);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        if (!await users.HasPasswordAsync(user))
        {
            return await ShowAsync(user);
        }

        if (string.IsNullOrEmpty(Password))
        {
            ModelState.AddModelError(nameof(Password), t["Validation.PasswordRequired"]);
            return await ShowAsync(user);
        }

        var check = await signIn.CheckPasswordSignInAsync(user, Password, lockoutOnFailure: true);
        if (!check.Succeeded)
        {
            ModelState.AddModelError("", t["Delete.WrongPassword"]);
            return await ShowAsync(user);
        }

        return await DeleteAndSignOutAsync(user);
    }

    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        if (await ceremonies.AssertAsync(Credential) is { User.Id: var id } && id == user.Id)
        {
            return await DeleteAndSignOutAsync(user);
        }

        ModelState.Clear();
        ModelState.AddModelError("", t["Delete.PasskeyFailed"]);
        return await ShowAsync(user);
    }

    private async Task<IActionResult> ShowAsync(PlayerAccount user)
    {
        HasPassword = await users.HasPasswordAsync(user);
        if ((await users.GetPasskeysAsync(user)).Count > 0)
        {
            PasskeyOptions = await ceremonies.RequestOptionsAsync(user);
        }

        return Page();
    }

    private async Task<IActionResult> DeleteAndSignOutAsync(PlayerAccount user)
    {
        // 003 T11: hier GameLoop.ForceLeave – Figur sofort entfernen, Verbindung mit Kicked(SIGNED_OUT) schließen (AK-16). Position dabei NICHT speichern (Account wird gelöscht, FK).
        var result = await users.DeleteAsync(user);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException("Account konnte nicht gelöscht werden: " + string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        await signIn.SignOutAsync();
        Deleted = true;
        return Page();
    }
}
