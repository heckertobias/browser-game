using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Data;

/// <summary>Spieler-Account. <see cref="IdentityUser.UserName"/> ist der Spielername (z. B. „Tobias“).</summary>
public sealed class Account : IdentityUser;

/// <summary>Zuletzt gespeicherte Position eines Accounts in der Welt.</summary>
public sealed class PlayerPosition
{
    public required string AccountId { get; init; }
    public double X { get; set; }
    public double Y { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// Gemeinsames Modell für alle Datenbanken (ADR 0004). Die abgeleiteten Kontexte existieren nur,
/// damit jeder Provider seinen eigenen Migrationssatz bekommt.
/// </summary>
public abstract class GameDbContext(DbContextOptions options) : IdentityDbContext<Account>(options), IDataProtectionKeyContext
{
    public DbSet<PlayerPosition> PlayerPositions => Set<PlayerPosition>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public DbSet<Setting> Settings => Set<Setting>();

    /// <summary>
    /// Identity-Schema mit Passkey-Tabelle. Identity liest die Version aus <c>IdentityOptions.Stores</c>;
    /// <see cref="DatabaseSetup"/> setzt sie zur Laufzeit und beim Erzeugen der Migrationen.
    /// </summary>
    public static readonly Version IdentitySchemaVersion = IdentitySchemaVersions.Version3;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<PlayerPosition>(position =>
        {
            position.HasKey(p => p.AccountId);
            position.HasOne<Account>().WithOne().HasForeignKey<PlayerPosition>(p => p.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Setting>().HasKey(s => s.Key);
    }
}

public sealed class SqliteGameDbContext(DbContextOptions<SqliteGameDbContext> options) : GameDbContext(options);

public sealed class PostgresGameDbContext(DbContextOptions<PostgresGameDbContext> options) : GameDbContext(options);
