using System.Globalization;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>Eigene Anmeldearten verwalten: Passkeys entfernen, Passwort setzen (003 AK-13b, AK-13c). Mindestens eine Anmeldeart bleibt.</summary>
[Authorize]
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class ManageModel(UserManager<PlayerAccount> users, SignInManager<PlayerAccount> signIn, IStringLocalizer<Texts> t) : PageModel
{
    public sealed record PasskeyRow(string Id, int Number, string CreatedOn);

    // Mehrere Handler: string?, sonst gilt die Eigenschaft als implizit erforderlich (englische Standardmeldung).
    [BindProperty]
    public string? PasskeyId { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmNewPassword { get; set; }

    public string Name { get; private set; } = "";

    public string Email { get; private set; } = "";

    public bool HasPassword { get; private set; }

    public bool CanRemove { get; private set; }

    public IReadOnlyList<PasskeyRow> Passkeys { get; private set; } = [];

    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await ShowAsync();

    public async Task<IActionResult> OnPostRemovePasskeyAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        var passkeys = await users.GetPasskeysAsync(user);
        var target = passkeys.FirstOrDefault(p => WebEncoders.Base64UrlEncode(p.CredentialId) == PasskeyId);
        if (target is null)
        {
            return await ShowAsync();
        }

        if (!AccountCredentials.CanRemoveCredential(await users.HasPasswordAsync(user), passkeys.Count))
        {
            ModelState.AddModelError("", t["Manage.LastCredential"]);
            return await ShowAsync();
        }

        var removed = await users.RemovePasskeyAsync(user, target.CredentialId);
        if (!removed.Succeeded)
        {
            foreach (var error in removed.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return await ShowAsync();
        }

        Message = t["Manage.PasskeyRemoved"];
        return await ShowAsync();
    }

    public async Task<IActionResult> OnPostSetPasswordAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        if (await users.HasPasswordAsync(user))
        {
            return await ShowAsync();
        }

        if (string.IsNullOrEmpty(NewPassword))
        {
            ModelState.AddModelError(nameof(NewPassword), t["Validation.NewPasswordRequired"]);
        }

        if (string.IsNullOrEmpty(ConfirmNewPassword))
        {
            ModelState.AddModelError(nameof(ConfirmNewPassword), t["Validation.ConfirmPasswordRequired"]);
        }
        else if (NewPassword != ConfirmNewPassword)
        {
            ModelState.AddModelError(nameof(ConfirmNewPassword), t["Register.PasswordMismatch"]);
        }

        if (!ModelState.IsValid)
        {
            return await ShowAsync();
        }

        var result = await users.AddPasswordAsync(user, NewPassword!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            return await ShowAsync();
        }

        // AddPasswordAsync ändert den Security-Stamp; ohne Erneuerung würde die eigene Sitzung enden.
        await signIn.RefreshSignInAsync(user);
        Message = t["Manage.PasswordSet"];
        return await ShowAsync();
    }

    private async Task<IActionResult> ShowAsync()
    {
        if (await users.GetUserAsync(User) is not { } user)
        {
            return Challenge();
        }

        Name = user.UserName ?? "";
        Email = user.Email ?? "";
        HasPassword = await users.HasPasswordAsync(user);
        var passkeys = (await users.GetPasskeysAsync(user)).OrderBy(p => p.CreatedAt).ThenBy(p => Convert.ToHexString(p.CredentialId)).ToList();
        Passkeys = [.. passkeys.Select((p, i) =>
            new PasskeyRow(WebEncoders.Base64UrlEncode(p.CredentialId), i + 1, p.CreatedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)))];
        CanRemove = AccountCredentials.CanRemoveCredential(HasPassword, passkeys.Count);
        return Page();
    }
}
