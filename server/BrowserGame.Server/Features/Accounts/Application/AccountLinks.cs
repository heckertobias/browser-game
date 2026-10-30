using System.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>
/// Absolute Links für Mails. Basis ist <c>App:PublicUrl</c>, nie der Host-Header (sonst ließen sich Links per
/// manipuliertem Host auf fremde Domains umbiegen). Nur in der Entwicklung fällt sie auf die aktuelle Anfrage zurück.
/// </summary>
public sealed class AccountLinks(IConfiguration configuration, IHttpContextAccessor http, IWebHostEnvironment environment)
{
    public string Absolute(string pathAndQuery)
    {
        var baseUrl = configuration["App:PublicUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException("App:PublicUrl fehlt.");
            }

            var request = http.HttpContext?.Request ?? throw new InvalidOperationException("Keine Anfrage für die Link-Basis.");
            baseUrl = $"{request.Scheme}://{request.Host}";
        }

        return baseUrl.TrimEnd('/') + pathAndQuery;
    }

    public static string EncodeToken(string token) => WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

    public static string? DecodeToken(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }

    /// <summary>Nur lokale Rücksprungziele (kein Open Redirect); Standard ist das Spiel.</summary>
    public static string SafeReturnUrl(string? returnUrl, Func<string, bool> isLocal) =>
        !string.IsNullOrEmpty(returnUrl) && isLocal(returnUrl) ? returnUrl : "/play";
}
