using System.Net;
using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

public static partial class HtmlTexts
{
    /// <summary>Sichtbarer Text einer Seite: ohne Skripte, Styles und Tags, HTML-Entitäten aufgelöst.</summary>
    public static string Visible(string html)
    {
        var withoutBlocks = BlockPattern().Replace(html, " ");
        return WebUtility.HtmlDecode(TagPattern().Replace(withoutBlocks, " "));
    }

    [GeneratedRegex("<(script|style)\\b.*?</\\1>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex BlockPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();
}
