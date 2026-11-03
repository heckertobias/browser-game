namespace BrowserGame.Server.Features.Game;

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

    /// <summary>
    /// Standard-Abstand, in dem alle Positionen gespeichert werden (003 AK-19: höchstens so viel Bewegung geht verloren).
    /// Überschreibbar mit <c>Game:PositionSaveIntervalSeconds</c> (z. B. in Tests).
    /// </summary>
    public const double PositionSaveIntervalSeconds = 5;

    public const string PositionSaveIntervalSecondsKey = "Game:PositionSaveIntervalSeconds";

    /// <summary>Sekunden in ganze Ticks, aufgerundet; negative Werte zählen als 0.</summary>
    public static uint ToTicks(double seconds) => (uint)Math.Ceiling(Math.Max(0, seconds) * TickRateHz);

    /// <summary>Größere Client-Nachrichten trennen die Verbindung.</summary>
    public const int MaxClientMessageBytes = 1024;

    /// <summary>Höchstlänge einer Chatnachricht in UTF-16-Einheiten (011 AK-9).</summary>
    public const int ChatMaxLength = 200;

    /// <summary>So viele Nachrichten enthält der Chat-Verlauf höchstens (011 AK-15).</summary>
    public const int ChatHistoryCount = 50;

    /// <summary>So lange bleibt eine Chatnachricht sichtbar (011 AK-15, AK-16).</summary>
    public const int ChatHistoryHours = 24;

    /// <summary>So viele Chatnachrichten nimmt der Server je Account im Zeitfenster an (011 AK-13).</summary>
    public const int ChatRateLimitCount = 5;

    /// <summary>Länge des Zeitfensters für <see cref="ChatRateLimitCount"/> (011 AK-13).</summary>
    public const int ChatRateLimitSeconds = 10;

    /// <summary>
    /// Kapazität des Chat-Channels je Verbindung. Der Tick leert ihn; über dem Tempolimit (<see cref="ChatRateLimitCount"/>),
    /// sodass nur eine Flut die ältesten Einträge verliert (011 AK-13).
    /// </summary>
    public const int ChatInboxCapacity = 8;

    /// <summary>
    /// So viele Pflichtnachrichten warten je Verbindung höchstens. Auslegung 100 Spieler am Tempolimit: Ein Stoß von 500 Nachrichten
    /// passt, und bei 50 Nachrichten/s übersteht ein Client etwa 20 s Hänger (011 AK-10). Darüber wird die Verbindung getrennt.
    /// </summary>
    public const int OutboxRequiredLimit = 1024;
}
