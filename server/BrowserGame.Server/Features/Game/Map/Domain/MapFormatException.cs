using System.Text.Json;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Kartendatei ungültig (018). <see cref="Location"/> ist der JSON-Pfad der Fundstelle, z. B. <c>$.rows[3][17]</c>;
/// bei ungültigem JSON ist <see cref="Line"/> die Zeile der Datei.
/// </summary>
internal sealed class MapFormatException : Exception
{
    public MapFormatException(string location, string reason)
        : base($"Kartendatei ungültig bei {location}: {reason}")
    {
        Location = location;
    }

    public MapFormatException(long line, JsonException inner)
        : base($"Kartendatei ist kein gültiges JSON (Zeile {line}).", inner)
    {
        Location = "$";
        Line = line;
    }

    public string Location { get; }

    public long? Line { get; }
}
