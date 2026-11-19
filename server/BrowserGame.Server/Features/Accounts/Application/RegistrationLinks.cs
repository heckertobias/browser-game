using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>
/// Code im Registrierungslink (003 AK-2, AK-3): E-Mail-Adresse und Ablaufzeit, geschützt per Data Protection; in der DB
/// entsteht nichts. Die Gültigkeit ist ein Parameter von <see cref="CreateCode"/>, damit andere Links (z. B. Einladungen)
/// denselben Code mit eigener Dauer nutzen können.
/// </summary>
public sealed class RegistrationLinks(IDataProtectionProvider dataProtection, IConfiguration configuration)
{
    public const int DefaultLifetimeMinutes = 30;

    private readonly ITimeLimitedDataProtector _protector =
        dataProtection.CreateProtector("BrowserGame.Registration").ToTimeLimitedDataProtector();

    /// <summary>Gültigkeit des Links aus der Registrierungsmail (<c>Registration:LinkLifetimeMinutes</c>).</summary>
    public int LifetimeMinutes => configuration.GetValue("Registration:LinkLifetimeMinutes", DefaultLifetimeMinutes);

    public string CreateCode(string email, TimeSpan lifetime) => _protector.Protect(email, lifetime);

    /// <summary>E-Mail-Adresse aus einem gültigen Code; <c>null</c>, wenn er fehlt, verändert oder abgelaufen ist.</summary>
    public string? EmailFrom(string? code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return null;
        }

        try
        {
            return _protector.Unprotect(code, out _);
        }
        catch (CryptographicException)
        {
            return null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
