using System.Diagnostics.CodeAnalysis;
using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Name → Command (015), Namen ohne Rücksicht auf Groß-/Kleinschreibung (plan.md Entscheidung 3).
/// Bewusst fest im Code, ohne Rechteprüfung und ohne Schalter oder Umgebungsabfrage (AK-9, AK-10): Jeder angemeldete
/// Spieler darf jeden Command, in jedem Betrieb. Die Rechte kommen mit einer eigenen Spec.
/// </summary>
internal sealed class CommandRegistry
{
    private readonly Dictionary<string, Command> _commands = new(StringComparer.OrdinalIgnoreCase);

    public CommandRegistry()
    {
        // HP und Mana auf das Maximum (AK-6).
        Register("fullHealth", new Command(0, static (context, _) =>
            context.World.SetVitals(context.Self.Id, context.World.MaxHp, context.World.MaxMana)));
        // Zurück an den Startpunkt, HP und Mana bleiben (AK-7).
        Register("resetPosition", new Command(0, static (context, _) => context.World.ResetToStart(context.Self)));
        // Pfeile der Blickrichtung nur im eigenen Bild umschalten; die Welt bleibt, der Server merkt sich nichts (017 AK-11–13).
        Register("toggleFacingArrows", new Command(0, static (context, _) =>
            context.Reply(new ServerMessage { ToggleFacingArrows = new ToggleFacingArrows() })));
    }

    /// <summary>Fügt einen Command hinzu (z. B. 014: <c>spawnEnemy</c>). Ein schon vergebener Name wirft.</summary>
    public void Register(string name, Command command) => _commands.Add(name, command);

    public bool TryGet(string name, [NotNullWhen(true)] out Command? command) => _commands.TryGetValue(name, out command);
}
