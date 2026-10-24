using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Data;

/// <summary>Admin-Account, vollständig getrennt von Spieler-Accounts (004 AK-2).</summary>
public sealed class AdminUser : IdentityUser;

/// <summary>Einmalige, befristete Einladung bzw. Einrichtungslink (<see cref="SetupCreator"/>). Gespeichert wird nur der Hash.</summary>
public sealed class AdminInvite
{
    public const string SetupCreator = "setup";

    public required string Id { get; init; }
    public required string TokenHash { get; init; }
    public required string CreatedBy { get; init; }
    public string? Email { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    public DateTimeOffset? UsedAt { get; set; }

    /// <summary>Während der Einrichtung schon angelegter, aber noch nicht aktiver Admin (Abbruch → mit demselben Link weiter).</summary>
    public string? PendingUserId { get; set; }
}

/// <summary>Protokoll: wer hat wann was getan – ohne Geheimnisse (004 AK-12).</summary>
public sealed class AdminAuditEntry
{
    public long Id { get; init; }
    public DateTimeOffset At { get; init; }
    public required string Admin { get; init; }
    public required string Action { get; init; }
    public string? Details { get; init; }
}

/// <summary>
/// Admin-Daten in derselben Datenbank wie das Spiel, aber eigene Tabellen (Präfix <c>Admin</c>) und eigene
/// Migrations-Historie (ADR 0006). Identity ohne Rollen; Schema v3 mit Passkeys.
/// </summary>
public abstract class AdminDbContext(DbContextOptions options) : IdentityUserContext<AdminUser>(options)
{
    public const string MigrationsHistoryTable = "__EFMigrationsHistory_Admin";

    public DbSet<AdminInvite> Invites => Set<AdminInvite>();

    public DbSet<AdminAuditEntry> AuditEntries => Set<AdminAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<AdminUser>(user =>
        {
            user.ToTable("AdminUsers");
            // Indexnamen sind datenbankweit eindeutig; Identity vergibt sonst dieselben wie bei den Spielern.
            user.HasIndex(u => u.NormalizedUserName).HasDatabaseName("AdminUserNameIndex");
            user.HasIndex(u => u.NormalizedEmail).HasDatabaseName("AdminEmailIndex");
        });
        builder.Entity<IdentityUserClaim<string>>().ToTable("AdminUserClaims");
        builder.Entity<IdentityUserLogin<string>>().ToTable("AdminUserLogins");
        builder.Entity<IdentityUserToken<string>>().ToTable("AdminUserTokens");
        builder.Entity<IdentityUserPasskey<string>>().ToTable("AdminUserPasskeys");

        builder.Entity<AdminInvite>(invite =>
        {
            invite.ToTable("AdminInvites");
            invite.HasIndex(i => i.TokenHash).IsUnique();
        });
        builder.Entity<AdminAuditEntry>(entry =>
        {
            entry.ToTable("AdminAuditEntries");
            entry.HasIndex(e => e.At);
        });
    }
}

public sealed class SqliteAdminDbContext(DbContextOptions<SqliteAdminDbContext> options) : AdminDbContext(options);

public sealed class PostgresAdminDbContext(DbContextOptions<PostgresAdminDbContext> options) : AdminDbContext(options);
