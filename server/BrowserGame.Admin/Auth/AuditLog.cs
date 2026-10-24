using BrowserGame.Data;

namespace BrowserGame.Admin.Auth;

/// <summary>Protokoll der Admin-Aktionen (004 AK-12). Details nie mit Geheimnissen füllen – nur Feldnamen o. Ä.</summary>
public sealed class AuditLog(AdminDbContext db, TimeProvider time)
{
    public async Task WriteAsync(string admin, string action, string? details = null, CancellationToken cancellationToken = default)
    {
        db.AuditEntries.Add(new AdminAuditEntry { At = time.GetUtcNow(), Admin = admin, Action = action, Details = details });
        await db.SaveChangesAsync(cancellationToken);
    }
}
