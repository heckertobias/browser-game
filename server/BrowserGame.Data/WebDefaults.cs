using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace BrowserGame.Data;

/// <summary>Gemeinsame Bausteine für Spiel und Admin, damit beide gleich gehärtet sind.</summary>
public static class WebDefaults
{
    /// <summary>
    /// Docker-HEALTHCHECK: Das chiseled Image hat kein curl, deshalb prüft sich der Dienst selbst.
    /// Liefert den Exit-Code, wenn mit <c>--healthcheck</c> gestartet, sonst <c>null</c>.
    /// </summary>
    public static async Task<int?> TryRunHealthProbeAsync(string[] args)
    {
        if (args is not ["--healthcheck", ..])
        {
            return null;
        }

        var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        try
        {
            using var response = await http.GetAsync(new Uri($"http://localhost:{port}/health"));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }

    /// <summary>
    /// JSON sicher in einen <c>&lt;script type="application/json"&gt;</c>-Block einbetten: <c>&lt;</c> wird zu <c>\u003c</c>
    /// (gleiches JSON, kann den Block aber nicht mit <c>&lt;/script&gt;</c> verlassen).
    /// </summary>
    public static string JsonForHtmlScript(string json) => json.Replace("<", "\\u003c", StringComparison.Ordinal);

    public const string AuthRateLimitPolicy = "auth";

    /// <summary>
    /// Standard: höchstens so viele Formular-Absendungen pro Minute und IP auf Anmelde-Seiten (003 AK-10, 004 AK-4).
    /// Überschreibbar mit <c>RateLimiting:AuthRequestsPerMinute</c> (z. B. in Tests).
    /// </summary>
    public const int AuthRequestsPerMinute = 10;

    /// <summary>Rate-Limit nur für POSTs (Anmelden, Registrieren, …) pro Client-IP; GETs bleiben frei.</summary>
    public static IServiceCollection AddAuthRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = configuration.GetValue("RateLimiting:AuthRequestsPerMinute", AuthRequestsPerMinute);
        return services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(AuthRateLimitPolicy, context =>
                HttpMethods.IsPost(context.Request.Method)
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 })
                    : RateLimitPartition.GetNoLimiter("get"));
        });
    }

    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "no-referrer";
            headers.ContentSecurityPolicy =
                "default-src 'self'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
            await next();
        });
}
