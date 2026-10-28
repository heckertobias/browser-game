using BrowserGame.Data;

namespace BrowserGame.Server.Game;

/// <summary>Wer <c>/ws</c> öffnen darf: nur Seiten unter <c>App:PublicUrl</c> (Schutz vor Cross-Site-WebSocket-Hijacking).</summary>
internal static class GameAccess
{
    /// <summary>Vite-Entwicklungsserver; leitet <c>/ws</c> an den Spielserver weiter.</summary>
    public static readonly Uri ViteDevServer = new("http://localhost:5173");

    /// <summary>
    /// Ohne <paramref name="publicUrl"/> gilt in der Entwicklung die Adresse der Anfrage selbst
    /// (wie bei Passkeys und Mail-Links). Außerhalb der Entwicklung zählt nur <paramref name="publicUrl"/>.
    /// </summary>
    public static bool IsAllowedOrigin(string? origin, Uri? publicUrl, bool isDevelopment, Uri requestOrigin)
    {
        if (publicUrl is not null && Matches(origin, publicUrl))
        {
            return true;
        }

        return isDevelopment && (Matches(origin, ViteDevServer) || (publicUrl is null && Matches(origin, requestOrigin)));
    }

    private static bool Matches(string? origin, Uri expected) => PasskeySetup.IsExpectedOrigin(origin, crossOrigin: false, expected);
}
