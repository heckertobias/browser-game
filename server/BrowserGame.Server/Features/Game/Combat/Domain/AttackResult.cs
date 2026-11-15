namespace BrowserGame.Server.Features.Game;

/// <summary>Ergebnis eines Angriffsversuchs (013). Nur <see cref="Ok"/> ändert den Spielstand.</summary>
internal enum AttackResult
{
    /// <summary>Angriff ausgeführt: Nahkampf hat getroffen bzw. Projektil ist gestartet; Cooldown läuft (AK-4, AK-7, AK-11).</summary>
    Ok,

    /// <summary>Weder Nah- noch Fernkampf.</summary>
    InvalidKind,

    /// <summary>Ziel unbekannt oder tot (AK-2).</summary>
    InvalidTarget,

    /// <summary>Allgemeiner Cooldown läuft noch (AK-11).</summary>
    Cooldown,

    /// <summary>Ziel außerhalb der Reichweite der Angriffsart (AK-5, AK-9).</summary>
    OutOfRange,

    /// <summary>Ziel nicht in Blickrichtung (AK-19).</summary>
    NotFacing,

    /// <summary>Zu wenig Mana für den Fernkampf (AK-8).</summary>
    NotEnoughMana,
}
