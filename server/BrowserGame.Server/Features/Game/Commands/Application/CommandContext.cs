using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Was ein Command bekommt (015): die Welt, nur den ausführenden Spieler und den Tick. Ein Ziel lässt sich nicht angeben,
/// ein Command trifft nur <see cref="Self"/> (AK-11).
/// </summary>
internal sealed record CommandContext(World World, Player Self, uint Tick)
{
    /// <summary>
    /// Schickt eine Nachricht nur an den Absender des Commands (017 AK-12). Ohne Angabe passiert nichts, damit bestehende
    /// Aufrufe ohne Verbindung (Tests) unverändert bleiben.
    /// </summary>
    public Action<ServerMessage> Reply { get; init; } = static _ => { };
}
