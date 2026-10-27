using Microsoft.AspNetCore.StaticFiles;

namespace BrowserGame.Server.Pwa;

/// <summary>Cache-Header für Static Files und /play-Fallbacks (007 AK-13). Der Fallback schreibt den Pfad auf /play/index.html um.</summary>
internal static class PwaCaching
{
    private static readonly HashSet<string> NoCache = new(StringComparer.OrdinalIgnoreCase)
        { "/play/index.html", ServiceWorkerScript.OfflinePage, "/manifest.webmanifest" };

    public static void Apply(StaticFileResponseContext context)
    {
        var path = context.Context.Request.Path;
        var headers = context.Context.Response.Headers;
        if (path.StartsWithSegments("/play/assets", StringComparison.OrdinalIgnoreCase))
            headers.CacheControl = "public, max-age=31536000, immutable"; // Dateinamen mit Inhalts-Hash
        else if (path.Value is { } value && NoCache.Contains(value))
            headers.CacheControl = "no-cache";
    }
}
