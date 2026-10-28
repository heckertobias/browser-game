namespace BrowserGame.Server.Game;

internal static class GameSettings
{
    /// <summary>Welteinheiten pro Sekunde.</summary>
    public const double Speed = 200;

    public const int TickRateHz = 20;

    /// <summary>
    /// Standard-Nachlauf: So lange bleibt eine getrennte Figur stehen (003 AK-20).
    /// Überschreibbar mit <c>Game:LingerSeconds</c> (z. B. in Tests).
    /// </summary>
    public const double LingerSeconds = 20;

    public const string LingerSecondsKey = "Game:LingerSeconds";

    /// <summary>Sekunden in ganze Ticks, aufgerundet; negative Werte zählen als 0.</summary>
    public static uint ToTicks(double seconds) => (uint)Math.Ceiling(Math.Max(0, seconds) * TickRateHz);

    /// <summary>Größere Client-Nachrichten trennen die Verbindung.</summary>
    public const int MaxClientMessageBytes = 1024;
}
