using System.Globalization;

namespace BrowserGame.Server.Features.Game;

/// <summary>Regeln für den Text einer Chatnachricht (011 AK-8, AK-9, AK-12). HTML und Links sind hier nur Zeichen.</summary>
internal static class ChatRules
{
    /// <summary>
    /// Ersetzt Steuerzeichen und Zeilenumbrüche durch je ein Leerzeichen und trimmt.
    /// Das Ergebnis zählt in UTF-16-Einheiten, wie <c>maxLength</c> im Browser.
    /// Leer oder länger als <paramref name="maxLength"/> ergibt <c>null</c>.
    /// </summary>
    public static string? Normalize(string text, int maxLength)
    {
        var chars = text.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (IsControlOrLineBreak(chars[i]))
            {
                chars[i] = ' ';
            }
        }

        var normalized = new string(chars).Trim();
        return normalized.Length == 0 || normalized.Length > maxLength ? null : normalized;
    }

    // char.IsControl kennt nur die Kategorie Cc, Zeilen- und Absatztrenner liegen in eigenen Kategorien.
    private static bool IsControlOrLineBreak(char c) =>
        char.IsControl(c) || char.GetUnicodeCategory(c) is UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator;
}
