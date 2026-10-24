using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;

namespace BrowserGame.Admin.Pages;

public enum InviteStep
{
    Invalid,
    Details,
    ChooseMethod,
    Totp,
    Done,
}

/// <summary>
/// Einladung bzw. Einrichtungslink einlösen (004 AK-1, AK-4b, AK-4c, AK-4e). Das Token im Link ist die einzige
/// Berechtigung und wird bei jedem Schritt mitgeschickt.
/// </summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed partial class InviteModel(
    AdminInvites invites, AdminDbContext db, UserManager<AdminUser> users, SignInManager<AdminUser> signIn, AuditLog audit,
    ILogger<InviteModel> logger) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    // Nullable: Nur der Details-Schritt schickt sie mit; sonst würde jede andere Absendung „required“ melden.
    [BindProperty]
    public string? Name { get; set; }

    [BindProperty]
    public string? Email { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    [BindProperty]
    public string? Code { get; set; }

    [BindProperty]
    public string? Credential { get; set; }

    public InviteStep Step { get; private set; } = InviteStep.Invalid;

    public string PasskeyOptions { get; private set; } = "{}";

    public string? TotpQrSvg { get; private set; }

    public string? TotpKey { get; private set; }

    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync() => await ShowAsync();

    public async Task<IActionResult> OnPostDetailsAsync()
    {
        if (await invites.FindValidAsync(Token) is not { } invite)
        {
            return await ShowAsync();
        }

        if (invite.PendingUserId is null)
        {
            var user = new AdminUser { UserName = Name?.Trim(), Email = (invite.Email ?? Email)?.Trim() };
            if (string.IsNullOrWhiteSpace(user.UserName) || string.IsNullOrWhiteSpace(user.Email))
            {
                ModelState.AddModelError("", "Bitte Name und E-Mail angeben.");
                return await ShowAsync(InviteStep.Details);
            }

            var created = await users.CreateAsync(user);
            if (!created.Succeeded)
            {
                foreach (var error in created.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return await ShowAsync(InviteStep.Details);
            }

            invite.PendingUserId = user.Id;
            await db.SaveChangesAsync();
        }

        return await ShowAsync();
    }

    public async Task<IActionResult> OnPostPasswordAsync()
    {
        if (await PendingAsync() is not { } pending)
        {
            return await ShowAsync();
        }

        if (string.IsNullOrEmpty(Password) || Password != ConfirmPassword)
        {
            ModelState.AddModelError("", "Die Passwörter stimmen nicht überein.");
            return await ShowAsync(InviteStep.ChooseMethod);
        }

        if (!await users.HasPasswordAsync(pending.User))
        {
            var added = await users.AddPasswordAsync(pending.User, Password);
            if (!added.Succeeded)
            {
                foreach (var error in added.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                return await ShowAsync(InviteStep.ChooseMethod);
            }
        }

        await users.ResetAuthenticatorKeyAsync(pending.User);
        return await ShowAsync();
    }

    public async Task<IActionResult> OnPostTotpAsync()
    {
        if (await PendingAsync() is not { } pending)
        {
            return await ShowAsync();
        }

        var code = (Code ?? "").Replace(" ", "", StringComparison.Ordinal);
        if (!await users.VerifyTwoFactorTokenAsync(pending.User, users.Options.Tokens.AuthenticatorTokenProvider, code))
        {
            ModelState.AddModelError("", "Der Code stimmt nicht. Prüfe die Uhrzeit deines Geräts und versuche es erneut.");
            return await ShowAsync(InviteStep.Totp);
        }

        await users.SetTwoFactorEnabledAsync(pending.User, true);
        var codes = await users.GenerateNewTwoFactorRecoveryCodesAsync(pending.User, 10);
        await CompleteAsync(pending.Invite, pending.User);
        RecoveryCodes = [.. codes ?? []];
        Step = InviteStep.Done;
        return Page();
    }

    public async Task<IActionResult> OnPostPasskeyAsync()
    {
        if (await PendingAsync() is not { } pending)
        {
            return await ShowAsync();
        }

        var attestation = string.IsNullOrEmpty(Credential) ? null : await signIn.PerformPasskeyAttestationAsync(Credential);
        if (attestation is not { Succeeded: true })
        {
            var reason = attestation?.Failure?.Message ?? "kein Passkey übermittelt";
            LogPasskeyFailed(reason);
            ModelState.AddModelError("", $"Der Passkey konnte nicht angelegt werden ({reason}). Bitte versuche es erneut.");
            return await ShowAsync(InviteStep.ChooseMethod);
        }

        await users.AddOrUpdatePasskeyAsync(pending.User, attestation.Passkey);
        await CompleteAsync(pending.Invite, pending.User);
        Step = InviteStep.Done;
        return Page();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey bei der Einrichtung abgelehnt: {Reason}")]
    private partial void LogPasskeyFailed(string reason);

    private async Task CompleteAsync(AdminInvite invite, AdminUser user)
    {
        await invites.MarkUsedAsync(invite);
        await audit.WriteAsync(user.UserName!, invite.CreatedBy == AdminInvite.SetupCreator ? "Erster Admin eingerichtet" : "Einladung eingelöst",
            invite.CreatedBy == AdminInvite.SetupCreator ? null : $"eingeladen von {invite.CreatedBy}");
        await signIn.SignInAsync(user, isPersistent: false);
    }

    private async Task<(AdminInvite Invite, AdminUser User)?> PendingAsync()
    {
        var invite = await invites.FindValidAsync(Token);
        var user = invite?.PendingUserId is { } id ? await users.FindByIdAsync(id) : null;
        return invite is not null && user is not null ? (invite, user) : null;
    }

    /// <summary>Zeigt den passenden Schritt; ohne Vorgabe wird er aus Einladung und angelegtem Account abgeleitet.</summary>
    private async Task<IActionResult> ShowAsync(InviteStep? step = null)
    {
        var invite = await invites.FindValidAsync(Token);
        if (invite is null)
        {
            Step = InviteStep.Invalid;
            return Page();
        }

        Email = invite.Email ?? Email;
        var user = invite.PendingUserId is { } id ? await users.FindByIdAsync(id) : null;
        Step = step ?? (user is null ? InviteStep.Details
            : await users.HasPasswordAsync(user) ? InviteStep.Totp
            : InviteStep.ChooseMethod);

        if (user is not null && Step == InviteStep.ChooseMethod)
        {
            PasskeyOptions = await signIn.MakePasskeyCreationOptionsAsync(new PasskeyUserEntity
            {
                Id = user.Id,
                Name = user.Email!,
                DisplayName = user.UserName!,
            });
        }

        if (user is not null && Step == InviteStep.Totp)
        {
            var key = await users.GetAuthenticatorKeyAsync(user);
            if (string.IsNullOrEmpty(key))
            {
                await users.ResetAuthenticatorKeyAsync(user);
                key = await users.GetAuthenticatorKeyAsync(user);
            }

            TotpKey = TotpSetup.Readable(key!);
            TotpQrSvg = TotpSetup.QrSvg(TotpSetup.OtpAuthUri(user.Email!, key!));
        }

        return Page();
    }
}
