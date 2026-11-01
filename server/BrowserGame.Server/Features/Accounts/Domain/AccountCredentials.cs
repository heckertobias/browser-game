namespace BrowserGame.Server.Features.Accounts;

/// <summary>Regel „mindestens eine Anmeldeart bleibt“ (003 AK-13c).</summary>
internal static class AccountCredentials
{
    /// <summary>Darf eine Anmeldeart (Passwort oder ein Passkey) entfernt werden? Nur wenn danach noch eine bleibt.</summary>
    public static bool CanRemoveCredential(bool hasPassword, int passkeyCount) => (hasPassword ? 1 : 0) + passkeyCount >= 2;
}
