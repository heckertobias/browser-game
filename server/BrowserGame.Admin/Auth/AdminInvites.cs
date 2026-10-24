using System.Security.Cryptography;
using System.Text;
using BrowserGame.Data;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Auth;

/// <summary>
/// Einmalige, befristete Einladungen (004 AK-1, AK-4e). In der DB liegt nur der SHA-256-Hash des Tokens; das Token
/// selbst steht ausschließlich im Link.
/// </summary>
public sealed partial class AdminInvites(
    AdminDbContext db, TimeProvider time, IConfiguration configuration, IHttpContextAccessor http, IWebHostEnvironment environment,
    ILogger<AdminInvites> logger)
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(48);

    public async Task<(AdminInvite Invite, string Link)> CreateAsync(string createdBy, string? email, CancellationToken cancellationToken = default)
    {
        var token = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var now = time.GetUtcNow();
        var invite = new AdminInvite
        {
            Id = Guid.NewGuid().ToString("N"),
            TokenHash = Hash(token),
            CreatedBy = createdBy,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        };
        db.Invites.Add(invite);
        await db.SaveChangesAsync(cancellationToken);
        return (invite, Link(token));
    }

    /// <summary>Gültige Einladung zum Token oder <c>null</c> (unbekannt, abgelaufen oder benutzt).</summary>
    public async Task<AdminInvite?> FindValidAsync(string? token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(token))
        {
            return null;
        }

        var hash = Hash(token);
        var invite = await db.Invites.FirstOrDefaultAsync(i => i.TokenHash == hash, cancellationToken);
        return invite is { UsedAt: null } && invite.ExpiresAt > time.GetUtcNow() ? invite : null;
    }

    public async Task MarkUsedAsync(AdminInvite invite, CancellationToken cancellationToken = default)
    {
        invite.UsedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Erster Start (AK-1): Gibt es keinen eingerichteten Admin, wird ein neuer Einrichtungslink erzeugt und einmal
    /// ins Log geschrieben; ältere, unbenutzte Einrichtungslinks werden dabei ungültig.
    /// </summary>
    public async Task EnsureSetupLinkAsync(AdminCredentials credentials, CancellationToken cancellationToken = default)
    {
        if (await credentials.AnyActiveAdminAsync(cancellationToken))
        {
            return;
        }

        var now = time.GetUtcNow();
        await db.Invites
            .Where(i => i.CreatedBy == AdminInvite.SetupCreator && i.UsedAt == null)
            .ExecuteUpdateAsync(set => set.SetProperty(i => i.ExpiresAt, now), cancellationToken);

        var (_, link) = await CreateAsync(AdminInvite.SetupCreator, email: null, cancellationToken);
        LogSetupLink(link, Lifetime.TotalHours);
    }

    internal static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private string Link(string token)
    {
        var baseUrl = configuration["App:PublicUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var request = http.HttpContext?.Request;
            baseUrl = environment.IsDevelopment() && request is not null ? $"{request.Scheme}://{request.Host}" : "http://localhost:5090";
        }

        return $"{baseUrl.TrimEnd('/')}/Invite?token={token}";
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Noch kein Admin eingerichtet. Einrichtungslink (einmalig, {Hours} h gültig): {Link}")]
    private partial void LogSetupLink(string link, double hours);
}
