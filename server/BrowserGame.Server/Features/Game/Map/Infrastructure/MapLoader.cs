using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Lädt die Karte des Gebiets einmal beim Start (018): <see cref="GameSettings.DefaultMapPath"/> neben der Anwendung oder
/// den Pfad aus <c>Game:MapPath</c>. Ist die Datei ungültig oder fehlt sie, steht der Grund im Log und der Server startet
/// nicht (Plan-Entscheidung 1).
/// </summary>
internal sealed partial class MapLoader
{
    // Optional wie die Uhr des GameLoops: Ein Container ohne Logging (Tests) lädt die Karte trotzdem.
    public MapLoader(IConfiguration configuration, ILogger<MapLoader>? logger = null)
    {
        ILogger log = logger ?? NullLogger<MapLoader>.Instance;
        var path = PathFrom(configuration);
        try
        {
            Map = Load(path);
        }
        catch (Exception e) when (e is MapFormatException or IOException or UnauthorizedAccessException)
        {
            LogInvalid(log, path, e.Message);
            throw;
        }

        LogLoaded(log, path, Map.Width, Map.Height);
    }

    public AreaMap Map { get; }

    /// <summary>Pfad der Kartendatei; ein relativer Pfad gilt relativ zur Anwendung.</summary>
    internal static string PathFrom(IConfiguration configuration)
    {
        var configured = configuration[GameSettings.MapPathKey];
        return Path.Combine(
            AppContext.BaseDirectory, string.IsNullOrWhiteSpace(configured) ? GameSettings.DefaultMapPath : configured);
    }

    internal static AreaMap Load(string path) => MapFile.Parse(File.ReadAllText(path));

    [LoggerMessage(Level = LogLevel.Information, Message = "Karte {Path} geladen: {Width} × {Height} Kacheln")]
    private static partial void LogLoaded(ILogger logger, string path, int width, int height);

    [LoggerMessage(Level = LogLevel.Critical, Message = "Karte {Path} ungültig, Server startet nicht: {Reason}")]
    private static partial void LogInvalid(ILogger logger, string path, string reason);
}
