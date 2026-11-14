namespace BrowserGame.Server.Features.Game;

internal static class GameSettings
{
    /// <summary>Welteinheiten pro Sekunde.</summary>
    public const double Speed = 200;

    public const int TickRateHz = 20;

    /// <summary>Max-HP einer Figur (012 AK-1).</summary>
    public const double MaxHp = World.DefaultMaxHp;

    /// <summary>Max-Mana einer Figur (012 AK-1).</summary>
    public const double MaxMana = World.DefaultMaxMana;

    /// <summary>Mana pro Sekunde; leer bis voll in 20 s (012 AK-3). HP regeneriert nicht (AK-4).</summary>
    public const double ManaRegenPerSecond = World.DefaultManaRegenPerSecond;

    /// <summary>Blickrichtung neuer Figuren in Bogenmaß; π/2 = nach unten (016 AK-4).</summary>
    public const double DefaultFacing = World.DefaultFacing;

    /// <summary>Kürzere Richtungsvektoren ändern die Blickrichtung nicht (016 AK-5).</summary>
    public const double MinFacingInput = World.DefaultMinFacingInput;

    /// <summary>Öffnungswinkel nach vorn in Grad (± die Hälfte um die Blickrichtung), für Nah- und Fernkampf (013 AK-19).</summary>
    public const double AttackArcDegrees = Combat.DefaultAttackArcDegrees;

    /// <summary>Nahkampfreichweite, Mittelpunkt zu Mittelpunkt in Welteinheiten (013 AK-4, AK-5).</summary>
    public const double MeleeRange = Combat.DefaultMeleeRange;

    /// <summary>Nahkampfschaden (013 AK-4).</summary>
    public const double MeleeDamage = Combat.DefaultMeleeDamage;

    /// <summary>Maximalreichweite des Fernkampfs, geprüft beim Start (013 AK-9).</summary>
    public const double RangedRange = Combat.DefaultRangedRange;

    /// <summary>Fernkampfschaden beim Einschlag (013 AK-7).</summary>
    public const double RangedDamage = Combat.DefaultRangedDamage;

    /// <summary>Mana je Fernkampf (013 AK-10).</summary>
    public const double RangedManaCost = Combat.DefaultRangedManaCost;

    /// <summary>Projektil-Tempo in Welteinheiten pro Sekunde (013 AK-7).</summary>
    public const double ProjectileSpeed = Combat.DefaultProjectileSpeed;

    /// <summary>Allgemeiner Cooldown nach einem erfolgreichen Angriff (013 AK-11).</summary>
    public const double GlobalCooldownSeconds = Combat.DefaultGlobalCooldownSeconds;

    /// <summary>Feste Position des Trainings-Dummys, x (013 AK-13).</summary>
    public const double DummyX = Combat.DefaultDummyX;

    /// <summary>Feste Position des Trainings-Dummys, y; außerhalb des Spawn-Radius (013 AK-13).</summary>
    public const double DummyY = Combat.DefaultDummyY;

    /// <summary>Start-HP des Trainings-Dummys (013 AK-13).</summary>
    public const double DummyMaxHp = Combat.DefaultDummyMaxHp;

    /// <summary>Verzögerung, bis der Dummy mit vollen HP zurückkehrt (013 AK-15).</summary>
    public const double DummyRespawnSeconds = Combat.DefaultDummyRespawnSeconds;

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
    /// Kapazität des Angriffs-Channels je Verbindung (013). Der Tick leert ihn; wegen des Cooldowns bringt mehr als ein
    /// Angriff pro Tick nichts, sodass nur eine Flut die ältesten Einträge verliert.
    /// </summary>
    public const int AttackInboxCapacity = 4;

    /// <summary>
    /// So viele Pflichtnachrichten warten je Verbindung höchstens. Auslegung 100 Spieler am Tempolimit: Ein Stoß von 500 Nachrichten
    /// passt, und bei 50 Nachrichten/s übersteht ein Client etwa 20 s Hänger (011 AK-10). Darüber wird die Verbindung getrennt.
    /// </summary>
    public const int OutboxRequiredLimit = 1024;

    /// <summary>In diesem Abstand schreibt der ChatStore wartende Nachrichten; so oft prüft er auch, ob Aufräumen fällig ist (011).</summary>
    public const int ChatSaveIntervalSeconds = 1;

    /// <summary>So viele Nachrichten schreibt der ChatStore höchstens in einer Transaktion (011).</summary>
    public const int ChatSaveBatchSize = 500;

    /// <summary>
    /// So viele Nachrichten warten höchstens auf das Schreiben; bei der Auslegung von 50 Nachrichten/s reicht das für 5 Minuten
    /// Datenbank-Ausfall. Darüber fallen die ältesten weg (011).
    /// </summary>
    public const int ChatSaveQueueCapacity = 15_000;

    /// <summary>So lange versucht der ChatStore denselben Stapel, danach gibt er ihn auf (011).</summary>
    public const int ChatSaveGiveUpMinutes = 5;

    /// <summary>Abstand zwischen zwei Aufräum-Läufen des ChatStore (011 AK-16).</summary>
    public const int ChatCleanupIntervalHours = 1;

    /// <summary>
    /// So viele Zeilen löscht der ChatStore je Durchlauf, also etwa je Sekunde. Der Regelfall (180.000 Zeilen je Stunde bei
    /// 50 Nachrichten/s) dauert so 18 s, 1,8 Mio. Zeilen drei Minuten, ohne andere Schreiber lange zu blockieren (011 AK-16).
    /// </summary>
    public const int ChatCleanupBatchSize = 10_000;
}
