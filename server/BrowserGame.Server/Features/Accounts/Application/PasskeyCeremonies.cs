using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>
/// Passkey-Zeremonien der Account-Seiten (ADR 0005). Fehlt der Zustand (Seite älter als 5 min, andere Passkey-Seite
/// im zweiten Tab) oder ist das Ergebnis ungültig: null statt Ausnahme, Grund ins Log.
/// </summary>
public sealed partial class PasskeyCeremonies(SignInManager<Account> signIn, ILogger<PasskeyCeremonies> logger)
{
    public static PasskeyUserEntity UserEntity(Account account) =>
        new() { Id = account.Id, Name = account.Email!, DisplayName = account.UserName! };

    public Task<string> CreationOptionsAsync(Account account) => signIn.MakePasskeyCreationOptionsAsync(UserEntity(account));

    public Task<string> RequestOptionsAsync(Account? account) => signIn.MakePasskeyRequestOptionsAsync(account);

    public async Task<PasskeyAttestationResult?> AttestAsync(string? credential)
    {
        if (string.IsNullOrEmpty(credential))
        {
            LogRejected("Anlegen", "kein Passkey übermittelt");
            return null;
        }

        PasskeyAttestationResult result;
        try
        {
            result = await signIn.PerformPasskeyAttestationAsync(credential);
        }
        catch (InvalidOperationException ex)
        {
            LogRejected("Anlegen", ex.Message);
            return null;
        }

        if (!result.Succeeded)
        {
            LogRejected("Anlegen", result.Failure?.Message ?? "unbekannt");
            return null;
        }

        return result;
    }

    public async Task<PasskeyAssertionResult<Account>?> AssertAsync(string? credential)
    {
        if (string.IsNullOrEmpty(credential))
        {
            LogRejected("Anmelden", "kein Passkey übermittelt");
            return null;
        }

        PasskeyAssertionResult<Account> result;
        try
        {
            result = await signIn.PerformPasskeyAssertionAsync(credential);
        }
        catch (InvalidOperationException ex)
        {
            LogRejected("Anmelden", ex.Message);
            return null;
        }

        if (!result.Succeeded)
        {
            LogRejected("Anmelden", result.Failure?.Message ?? "unbekannt");
            return null;
        }

        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Passkey abgelehnt ({Operation}): {Reason}")]
    private partial void LogRejected(string operation, string reason);
}
