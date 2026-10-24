using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Auth;

/// <summary>Regeln zu Anmeldearten der Admins: nur Passkey oder Passwort mit 2FA zählt (004 AK-4b, AK-4f).</summary>
public sealed class AdminCredentials(AdminDbContext db, UserManager<AdminUser> users)
{
    /// <summary>Eingerichtet = mindestens ein Passkey oder Passwort mit aktivem zweitem Faktor.</summary>
    public static bool IsActive(bool hasPassword, bool twoFactorEnabled, int passkeys) =>
        passkeys > 0 || (hasPassword && twoFactorEnabled);

    public async Task<bool> IsActiveAsync(AdminUser user) =>
        IsActive(await users.HasPasswordAsync(user), user.TwoFactorEnabled, (await users.GetPasskeysAsync(user)).Count);

    public async Task<bool> AnyActiveAdminAsync(CancellationToken cancellationToken = default) =>
        await ActiveAdminIdsAsync(cancellationToken) is { Count: > 0 };

    public async Task<IReadOnlyList<string>> ActiveAdminIdsAsync(CancellationToken cancellationToken = default)
    {
        var withPasskey = db.Set<IdentityUserPasskey<string>>().Select(p => p.UserId);
        return await db.Users
            .Where(u => (u.PasswordHash != null && u.TwoFactorEnabled) || withPasskey.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }
}
