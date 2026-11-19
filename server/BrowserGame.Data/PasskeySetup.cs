using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Data;

/// <summary>
/// Passkey-Einstellungen für Spiel und Admin. Die Herkunftsprüfung vergleicht mit <c>App:PublicUrl</c> statt mit dem
/// <c>Origin</c>-Header der Anfrage: Wegen <c>Referrer-Policy: no-referrer</c> schicken Browser bei Formular-POSTs
/// <c>Origin: null</c>, die Standardprüfung von Identity schlüge dann immer fehl.
/// </summary>
public static class PasskeySetup
{
    /// <summary>
    /// Passkey-Optionen: optional eigene Server-Domain (<c>Passkeys:ServerDomain</c>), Herkunft gleich <c>App:PublicUrl</c>.
    /// Ohne <c>App:PublicUrl</c> gilt in der Entwicklung die Adresse der Anfrage, sonst wird jede Herkunft abgelehnt.
    /// </summary>
    public static IServiceCollection AddPasskeyOptions(this IServiceCollection services, IConfiguration configuration, bool isDevelopment)
    {
        var publicUrl = configuration["App:PublicUrl"];
        Uri? expected = string.IsNullOrWhiteSpace(publicUrl) ? null : new Uri(publicUrl);

        return services.Configure<IdentityPasskeyOptions>(options =>
        {
            var domain = configuration["Passkeys:ServerDomain"];
            if (!string.IsNullOrWhiteSpace(domain))
            {
                options.ServerDomain = domain;
            }

            options.ValidateOrigin = context =>
            {
                var target = expected ?? (isDevelopment ? RequestOrigin(context.HttpContext.Request) : null);
                return ValueTask.FromResult(target is not null && IsExpectedOrigin(context.Origin, context.CrossOrigin, target));
            };
        });
    }

    /// <summary>Exakt gleiches Schema, Host und Port; keine Einbettung in fremde Seiten (iframes).</summary>
    public static bool IsExpectedOrigin(string? origin, bool crossOrigin, Uri expected) =>
        !crossOrigin
        && Uri.TryCreate(origin, UriKind.Absolute, out var actual)
        && Uri.Compare(actual, expected, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0;

    private static Uri RequestOrigin(HttpRequest request) => new($"{request.Scheme}://{request.Host}");
}
