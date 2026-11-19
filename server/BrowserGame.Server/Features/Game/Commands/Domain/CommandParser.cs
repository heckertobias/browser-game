namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Zerlegt Chat-Text, der mit <c>/</c> beginnt, in einen Command-Aufruf (015 AK-3, AK-4). Ohne Netz, ohne DB.
/// Form: <c>/run name</c> oder <c>/run name(arg, …)</c>, Leerraum zwischen den Teilen erlaubt.
/// </summary>
internal static class CommandParser
{
    private const string RunPrefix = "run";

    public static ParseResult Parse(string text)
    {
        if (!text.StartsWith('/'))
        {
            return new ParseResult.UnknownPrefix();
        }

        var i = 1;
        while (i < text.Length && !char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        if (!text.AsSpan(1, i - 1).Equals(RunPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new ParseResult.UnknownPrefix();
        }

        i = SkipWhitespace(text, i);
        if (i == text.Length || !char.IsAsciiLetter(text[i]))
        {
            return new ParseResult.Invalid();
        }

        var nameStart = i;
        while (i < text.Length && char.IsAsciiLetterOrDigit(text[i]))
        {
            i++;
        }

        var name = text[nameStart..i];

        i = SkipWhitespace(text, i);
        if (i == text.Length)
        {
            return new ParseResult.Ok(name, []);
        }

        if (text[i] != '(')
        {
            return new ParseResult.Invalid();
        }

        var close = text.IndexOf(')', i + 1);
        if (close < 0 || SkipWhitespace(text, close + 1) != text.Length)
        {
            return new ParseResult.Invalid();
        }

        var inner = text[(i + 1)..close];
        if (inner.Trim().Length == 0)
        {
            return new ParseResult.Ok(name, []);
        }

        var args = inner.Split(',');
        for (var k = 0; k < args.Length; k++)
        {
            args[k] = args[k].Trim();
            if (args[k].Length == 0)
            {
                return new ParseResult.Invalid();
            }
        }

        return new ParseResult.Ok(name, args);
    }

    private static int SkipWhitespace(string text, int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }

        return i;
    }
}
