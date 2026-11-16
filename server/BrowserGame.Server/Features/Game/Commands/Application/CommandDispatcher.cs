using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Führt Chat-Text mit <c>/</c> als Command aus (015): parsen, nachschlagen, Parameterzahl prüfen, ausführen.
/// Liefert <c>null</c> bei Erfolg, sonst den Grund der Ablehnung (AK-4); abgelehnt ändert sich nichts.
/// </summary>
internal sealed class CommandDispatcher(CommandRegistry registry)
{
    public CommandRejected.Types.Reason? Execute(string text, CommandContext context)
    {
        switch (CommandParser.Parse(text))
        {
            case ParseResult.Ok ok:
                if (!registry.TryGet(ok.Name, out var command))
                {
                    return CommandRejected.Types.Reason.UnknownCommand;
                }

                if (ok.Args.Count != command.ParameterCount)
                {
                    return CommandRejected.Types.Reason.Invalid;
                }

                command.Run(context, ok.Args);
                return null;
            case ParseResult.Invalid:
                return CommandRejected.Types.Reason.Invalid;
            default:
                return CommandRejected.Types.Reason.UnknownCommand;
        }
    }
}
