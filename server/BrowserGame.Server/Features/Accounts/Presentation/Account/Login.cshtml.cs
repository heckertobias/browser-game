using System.ComponentModel.DataAnnotations;
using BrowserGame.Data;
using BrowserGame.Server.Features.Accounts;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Localization.Resources;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Localization;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Features.Accounts.Presentation.Account;

/// <summary>Anmeldung mit E-Mail und Passwort (003 AK-1, AK-9). Alle Fehlerfälle zeigen dieselbe Meldung.</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class LoginModel(
    SignInManager<PlayerAccount> signIn, UserManager<PlayerAccount> users, PasskeyCeremonies ceremonies, IStringLocalizer<Texts> t) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    [BindProperty]
    [Required(ErrorMessage = "Validation.PasswordRequired")]
    public string Password { get; set; } = "";

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    // Mehrere Handler: string?, sonst gilt die Eigenschaft als implizit erforderlich.
    [BindProperty]
    public string? Credential { get; set; }

    /// <summary>WebAuthn-Optionen für „Mit Passkey anmelden“ (ohne vorher bekannten Nutzer, discoverable).</summary>
    public string PasskeyOptions { get; private set; } = "{}";

    public async Task OnGetAsync() => PasskeyOptions = await ceremonies.RequestOptionsAsync(null);

    public async Task<IActionResult> OnPostAsync()
    {
        if (ModelState.IsValid && await users.FindByEmailAsync(Email.Trim()) is { } user)
        {
            var result = await signIn.PasswordSignInAsync(user, Password, isPersistent: true, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                // 006 AK-6: Ab der Anmeldung gilt die Sprache des Accounts, auch gegen ein vorhandenes Cookie.
                // Die Passkey-Anmeldung (OnPostPasskeyAsync) tut dasselbe.
                LanguageCookie.Append(Response, user.Language);
                return LocalRedirect(AccountLinks.SafeReturnUrl(ReturnUrl, Url.IsLocalUrl));
            }
        }

        ModelState.Clear();
        ModelState.AddModelError("", t["Login.GenericError"]);
        await OnGetAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        ModelState.Clear();
        if (await ceremonies.AssertAsync(Credential) is { User: { } user, Passkey: { } passkey })
        {
            if (!await signIn.CanSignInAsync(user) || await users.IsLockedOutAsync(user))
            {
                ModelState.AddModelError("", t["Login.PasskeyFailed"]);
            }
            else
            {
                await users.AddOrUpdatePasskeyAsync(user, passkey); // Sign-Count wie bei PasskeySignInAsync
                await signIn.SignInAsync(user, isPersistent: true); // AK-11; PasskeySignInAsync wäre nicht persistent
                LanguageCookie.Append(Response, user.Language); // 006 AK-6
                return LocalRedirect(AccountLinks.SafeReturnUrl(ReturnUrl, Url.IsLocalUrl));
            }
        }
        else
        {
            ModelState.AddModelError("", t["Login.PasskeyFailed"]);
        }

        await OnGetAsync();
        return Page();
    }
}
