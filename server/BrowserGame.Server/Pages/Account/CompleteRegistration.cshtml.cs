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
/// Schritt 2 der Registrierung über den Link aus der Mail (003 AK-2 bis AK-7b, AK-11; 006 AK-7): erst der Spielername,
/// dann Passwort oder Passkey. Erst hier entsteht der Account – bestätigt und angemeldet. Gibt es die Adresse schon,
/// ist der Link wertlos. Alle Formulare posten an die Link-URL; welcher Schritt, steht in <see cref="Step"/>.
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class CompleteRegistrationModel(
    UserManager<PlayerAccount> users, SignInManager<PlayerAccount> signIn, PasskeyCeremonies ceremonies,
    RegistrationLinks registrationLinks, IStringLocalizer<Texts> t) : PageModel
{
    public const string PasswordStep = "password";
    public const string PasskeyStep = "passkey";

    // Mehrere Schritte: string?, sonst gilt die Eigenschaft als implizit erforderlich.
    [BindProperty(SupportsGet = true)]
    public string? Code { get; set; }

    [BindProperty]
    public string? Step { get; set; }

    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    [BindProperty]
    public string? Credential { get; set; }

    public bool Invalid { get; private set; }

    public string Email { get; private set; } = "";

    /// <summary>Geprüfter Name; gesetzt heißt: Anmeldeart wählen.</summary>
    public string? ChosenName { get; private set; }

    public string PasskeyOptions { get; private set; } = "{}";

    public async Task OnGetAsync() => Invalid = !await LoadEmailAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (!await LoadEmailAsync())
        {
            Invalid = true;
            return Page();
        }

        if (await ValidNameAsync() is not { } name)
        {
            return Page();
        }

        return Step switch
        {
            PasswordStep => await CreateWithPasswordAsync(name),
            PasskeyStep => await CreateWithPasskeyAsync(name),
            _ => await ShowCredentialStepAsync(name),
        };
    }

    private async Task<bool> LoadEmailAsync()
    {
        if (registrationLinks.EmailFrom(Code) is not { } email || await users.FindByEmailAsync(email) is not null)
        {
            return false;
        }

        Email = email;
        return true;
    }

    /// <summary>Regeln wie AK-5 bis AK-7b; bei jedem Schritt erneut, weil der Name aus einem versteckten Feld kommt.</summary>
    private async Task<string?> ValidNameAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), t["Validation.NameRequired"]);
            return null;
        }

        var error = PlayerNames.TryNormalize(Name, out var name);
        if (error != PlayerNameError.None)
        {
            ModelState.AddModelError(nameof(Name), PlayerNames.Describe(error));
            return null;
        }

        if (await users.FindByNameAsync(name) is not null)
        {
            ModelState.AddModelError(nameof(Name), users.ErrorDescriber.DuplicateUserName(name).Description);
            return null;
        }

        return name;
    }

    private async Task<IActionResult> ShowCredentialStepAsync(string name)
    {
        ChosenName = name;
        // Ungespeicherter Entwurf mit frischer Id: Sie steht im Passkey und wird beim Anlegen die Account-ID.
        PasskeyOptions = await ceremonies.CreationOptionsAsync(new PlayerAccount { UserName = name, Email = Email });
        return Page();
    }

    private async Task<IActionResult> CreateWithPasswordAsync(string name)
    {
        if (string.IsNullOrEmpty(Password))
        {
            ModelState.AddModelError(nameof(Password), t["Validation.PasswordRequired"]);
        }

        if (string.IsNullOrEmpty(ConfirmPassword))
        {
            ModelState.AddModelError(nameof(ConfirmPassword), t["Validation.ConfirmPasswordRequired"]);
        }
        else if (!string.IsNullOrEmpty(Password) && Password != ConfirmPassword)
        {
            ModelState.AddModelError(nameof(ConfirmPassword), t["Register.PasswordMismatch"]);
        }

        if (!ModelState.IsValid)
        {
            return await ShowCredentialStepAsync(name);
        }

        var account = NewAccount(name, id: null);
        var created = await users.CreateAsync(account, Password!);
        return created.Succeeded ? await SignInAndPlayAsync(account) : await ShowErrorsAsync(created, name);
    }

    private async Task<IActionResult> CreateWithPasskeyAsync(string name)
    {
        if (await ceremonies.AttestAsync(Credential) is not { Passkey: { } passkey, UserEntity: { } entity }
            || !string.Equals(entity.Name, Email, StringComparison.Ordinal)
            || !string.Equals(entity.DisplayName, name, StringComparison.Ordinal))
        {
            ModelState.AddModelError("", t["CompleteRegistration.PasskeyFailed"]);
            return await ShowCredentialStepAsync(name);
        }

        var account = NewAccount(name, entity.Id);
        var created = await users.CreateAsync(account);
        if (!created.Succeeded)
        {
            return await ShowErrorsAsync(created, name);
        }

        if (!(await users.AddOrUpdatePasskeyAsync(account, passkey)).Succeeded)
        {
            await users.DeleteAsync(account); // kein Account ohne Anmeldeart (AK-13c)
            ModelState.AddModelError("", t["CompleteRegistration.PasskeyFailed"]);
            return await ShowCredentialStepAsync(name);
        }

        return await SignInAndPlayAsync(account);
    }

    private PlayerAccount NewAccount(string name, string? id)
    {
        var account = new PlayerAccount
        {
            UserName = name,
            Email = Email,
            EmailConfirmed = true, // Der Link hat die Adresse bestätigt (AK-2).
            Language = GameLanguages.Normalize(CultureInfo.CurrentUICulture.Name), // 006 AK-7
        };
        if (id is not null)
        {
            account.Id = id;
        }

        return account;
    }

    private async Task<IActionResult> ShowErrorsAsync(IdentityResult result, string name)
    {
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateEmail)))
        {
            Invalid = true; // inzwischen registriert: Der Link ist verbraucht.
            return Page();
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName))
            ? Page()
            : await ShowCredentialStepAsync(name);
    }

    private async Task<IActionResult> SignInAndPlayAsync(PlayerAccount account)
    {
        await signIn.SignInAsync(account, isPersistent: true); // AK-11
        LanguageCookie.Append(Response, account.Language); // 006 AK-6
        return LocalRedirect("/play");
    }
}
