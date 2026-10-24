namespace BrowserGame.Server.Game;

internal static class GameSettings
{
    /// <summary>Welteinheiten pro Sekunde.</summary>
    public const double Speed = 200;

    public const int TickRateHz = 20;

    /// <summary>Größere Client-Nachrichten trennen die Verbindung.</summary>
    public const int MaxClientMessageBytes = 1024;
}
