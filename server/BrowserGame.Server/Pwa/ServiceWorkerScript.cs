using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BrowserGame.Server.Pwa;

/// <summary>GET /sw.js: Service Worker aus der eingebetteten Vorlage (007 AK-12, AK-13).</summary>
internal static partial class ServiceWorkerScript
{
    public const string OfflinePage = "/play/offline.html";
    private const string OfflineFile = "play/offline.html";
    private static readonly string Template = LoadTemplate();

    /// <summary>Reine Funktion. Version = erste 16 Hex-Zeichen von SHA-256 über das HTML; ohne HTML "none" und leerer Precache.</summary>
    public static string Render(string? offlineHtml)
    {
        var version = "none";
        string[] precache = [];
        if (offlineHtml is not null)
        {
            version = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(offlineHtml)))[..16];
            precache = [OfflinePage, .. AssetPath().Matches(offlineHtml).Select(m => m.Value).Distinct(StringComparer.Ordinal)];
        }
        // Erst VERSION, dann PRECACHE: eingesetztes JSON wird nicht erneut ersetzt.
        return Template
            .Replace("__VERSION__", version, StringComparison.Ordinal)
            .Replace("__PRECACHE__", JsonSerializer.Serialize(precache), StringComparison.Ordinal);
    }

    public static async Task HandleAsync(HttpContext context, IWebHostEnvironment environment)
    {
        string? html = null;
        var file = environment.WebRootFileProvider.GetFileInfo(OfflineFile);
        if (file.Exists)
        {
            await using var stream = file.CreateReadStream();
            using var reader = new StreamReader(stream);
            html = await reader.ReadToEndAsync(context.RequestAborted);
        }
        context.Response.ContentType = "text/javascript; charset=utf-8";
        context.Response.Headers.CacheControl = "no-cache";
        await context.Response.WriteAsync(Render(html), context.RequestAborted);
    }

    [GeneratedRegex("/play/assets/[A-Za-z0-9._-]+")]
    private static partial Regex AssetPath();

    private static string LoadTemplate()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BrowserGame.Server.Pwa.service-worker.js")
            ?? throw new InvalidOperationException("Vorlage service-worker.js fehlt.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
