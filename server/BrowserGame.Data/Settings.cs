using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Data;

/// <summary>Einstellung als Schlüssel/Wert (JSON), gepflegt im Admin-Interface (004).</summary>
public sealed class Setting
{
    public required string Key { get; init; }
    public required string Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public enum SmtpSecurity
{
    /// <summary>Je nach Port: 465 → SSL, sonst STARTTLS, falls angeboten.</summary>
    Auto,
    SslOnConnect,
    StartTls,
    None,
}

/// <summary>Zugang zum Mailserver. <see cref="Password"/> ist beim Lesen entschlüsselt, beim Speichern bedeutet leer „unverändert“.</summary>
public sealed record SmtpSettings(
    string Host,
    int Port,
    SmtpSecurity Security,
    string? Username,
    string? Password,
    string FromAddress,
    string? FromName)
{
    public bool HasPassword => !string.IsNullOrEmpty(Password);
}

/// <summary>Liest und schreibt die SMTP-Einstellungen; das Passwort liegt nur verschlüsselt in der DB (004 AK-7, AK-8).</summary>
public sealed class SmtpSettingsStore(GameDbContext db, IDataProtectionProvider dataProtection, TimeProvider time)
{
    internal const string Key = "Smtp";
    internal const string ProtectionPurpose = "Settings.Smtp";

    private readonly IDataProtector _protector = dataProtection.CreateProtector(ProtectionPurpose);

    public async Task<SmtpSettings?> GetAsync(CancellationToken cancellationToken = default)
    {
        var row = await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == Key, cancellationToken);
        if (row is null)
        {
            return null;
        }

        var stored = JsonSerializer.Deserialize<StoredSmtp>(row.Value)!;
        var password = stored.ProtectedPassword is null ? null : _protector.Unprotect(stored.ProtectedPassword);
        return new SmtpSettings(stored.Host, stored.Port, stored.Security, stored.Username, password, stored.FromAddress, stored.FromName);
    }

    /// <summary>Speichert die Einstellungen; ist <c>Password</c> leer, bleibt das gespeicherte erhalten. Liefert die geänderten Felder (fürs Protokoll, ohne Werte).</summary>
    public async Task<IReadOnlyList<string>> SaveAsync(SmtpSettings settings, CancellationToken cancellationToken = default)
    {
        var current = await GetAsync(cancellationToken);
        var password = settings.HasPassword ? settings.Password : current?.Password;

        var changed = new List<string>();
        void Compare<T>(string field, T? before, T? after)
        {
            if (current is null || !EqualityComparer<T>.Default.Equals(before, after))
            {
                changed.Add(field);
            }
        }

        Compare(nameof(SmtpSettings.Host), current?.Host, settings.Host);
        Compare(nameof(SmtpSettings.Port), current?.Port, settings.Port);
        Compare(nameof(SmtpSettings.Security), current?.Security, settings.Security);
        Compare(nameof(SmtpSettings.Username), current?.Username, settings.Username);
        Compare(nameof(SmtpSettings.Password), current?.Password, password);
        Compare(nameof(SmtpSettings.FromAddress), current?.FromAddress, settings.FromAddress);
        Compare(nameof(SmtpSettings.FromName), current?.FromName, settings.FromName);

        var stored = new StoredSmtp(
            settings.Host.Trim(), settings.Port, settings.Security, settings.Username?.Trim(),
            password is null ? null : _protector.Protect(password),
            settings.FromAddress.Trim(), settings.FromName?.Trim());

        var row = await db.Settings.FirstOrDefaultAsync(s => s.Key == Key, cancellationToken);
        if (row is null)
        {
            db.Settings.Add(new Setting { Key = Key, Value = JsonSerializer.Serialize(stored), UpdatedAt = time.GetUtcNow() });
        }
        else
        {
            row.Value = JsonSerializer.Serialize(stored);
            row.UpdatedAt = time.GetUtcNow();
        }

        await db.SaveChangesAsync(cancellationToken);
        return changed;
    }

    private sealed record StoredSmtp(
        string Host, int Port, SmtpSecurity Security, string? Username, string? ProtectedPassword, string FromAddress, string? FromName);
}
