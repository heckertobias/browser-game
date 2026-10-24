using Microsoft.AspNetCore.Builder;

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
