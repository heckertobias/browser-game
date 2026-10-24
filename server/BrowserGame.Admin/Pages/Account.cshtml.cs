using BrowserGame.Admin.Auth;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace BrowserGame.Admin.Pages;

/// <summary>Eigene Anmeldearten verwalten: Passkeys, Wiederherstellungscodes (004 AK-4b, AK-4c). Mindestens eine bleibt.</summary>
public sealed class AccountModel(UserManager<AdminUser> users, SignInManager<AdminUser> signIn, AuditLog audit) : PageModel
{
    public sealed record PasskeyRow(string Id, string Label);

    [BindProperty]
    public string? Credential { get; set; }

    [BindProperty]
    public string? PasskeyId { get; set; }

    public IReadOnlyList<PasskeyRow> Passkeys { get; private set; } = [];

    public bool HasPassword { get; private set; }

    public bool TwoFactorEnabled { get; private set; }

    public string PasskeyOptions { get; private set; } = "{}";

    public IReadOnlyList<string> NewRecoveryCodes { get; private set; } = [];

    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync() => await ShowAsync();

    public async Task<IActionResult> OnPostAddPasskeyAsync()
    {
        var user = await CurrentAsync();
        var attestation = string.IsNullOrEmpty(Credential) ? null : await signIn.PerformPasskeyAttestationAsync(Credential);
        if (attestation is not { Succeeded: true })
        {
            ModelState.AddModelError("", "Der Passkey konnte nicht angelegt werden.");
            return await ShowAsync();
        }

        await users.AddOrUpdatePasskeyAsync(user, attestation.Passkey);
        await audit.WriteAsync(user.UserName!, "Passkey hinzugefügt");
        Message = "Passkey hinzugefügt.";
        return await ShowAsync();
    }

    public async Task<IActionResult> OnPostRemovePasskeyAsync()
    {
        var user = await CurrentAsync();
        var passkeys = await users.GetPasskeysAsync(user);
        var target = passkeys.FirstOrDefault(p => WebEncoders.Base64UrlEncode(p.CredentialId) == PasskeyId);
        if (target is null)
        {
            return await ShowAsync();
        }

        if (!AdminCredentials.IsActive(await users.HasPasswordAsync(user), user.TwoFactorEnabled, passkeys.Count - 1))
        {
            ModelState.AddModelError("", "Das ist deine letzte Anmeldeart – lege erst einen weiteren Passkey an.");
            return await ShowAsync();
        }

        await users.RemovePasskeyAsync(user, target.CredentialId);
        await audit.WriteAsync(user.UserName!, "Passkey entfernt");
        Message = "Passkey entfernt.";
        return await ShowAsync();
    }

    public async Task<IActionResult> OnPostRecoveryCodesAsync()
    {
        var user = await CurrentAsync();
        if (!user.TwoFactorEnabled)
        {
            return await ShowAsync();
        }

        NewRecoveryCodes = [.. await users.GenerateNewTwoFactorRecoveryCodesAsync(user, 10) ?? []];
        await audit.WriteAsync(user.UserName!, "Wiederherstellungscodes erneuert");
        return await ShowAsync();
    }

    private async Task<AdminUser> CurrentAsync() =>
        await users.GetUserAsync(User) ?? throw new InvalidOperationException("Angemeldeter Admin nicht gefunden.");

    private async Task<IActionResult> ShowAsync()
    {
        var user = await CurrentAsync();
        HasPassword = await users.HasPasswordAsync(user);
        TwoFactorEnabled = user.TwoFactorEnabled;
        Passkeys = [.. (await users.GetPasskeysAsync(user)).Select(p =>
            new PasskeyRow(WebEncoders.Base64UrlEncode(p.CredentialId), p.Name ?? $"Passkey vom {p.CreatedAt.LocalDateTime:dd.MM.yyyy HH:mm}"))];
        PasskeyOptions = await signIn.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
        {
            Id = user.Id,
            Name = user.Email!,
            DisplayName = user.UserName!,
        });
        return Page();
    }
}
