namespace BrowserGame.Server.Features.Game;

/// <summary>Ergebnis von <see cref="CommandParser.Parse"/> (015). Nur <see cref="Ok"/> nennt einen Command.</summary>
internal abstract record ParseResult
{
    /// <summary><c>/run name(args)</c> ist gültig aufgebaut; ob es den Namen gibt, prüft erst die Registry.</summary>
    public sealed record Ok(string Name, IReadOnlyList<string> Args) : ParseResult;

    /// <summary>Anderes Präfix als <c>/run</c>, z. B. <c>/xyz</c> (AK-4).</summary>
    public sealed record UnknownPrefix : ParseResult;

    /// <summary><c>/run</c> ohne Namen oder mit kaputter Syntax (AK-4, „fehlerhafter Befehl“).</summary>
    public sealed record Invalid : ParseResult;
}
