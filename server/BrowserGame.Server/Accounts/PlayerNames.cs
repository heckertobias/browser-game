using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using BrowserGame.Server.Localization;

namespace BrowserGame.Server.Accounts;

internal enum PlayerNameError
{
    None,
    Length,
    Characters,
    Blocked,
}

/// <summary>Regeln für Spielernamen (003 AK-5, AK-6, AK-7b). Eindeutigkeit prüft Identity über den normalisierten Namen.</summary>
internal static partial class PlayerNames
{
    public const int MinLength = 3;
    public const int MaxLength = 16;

    private static readonly HashSet<string> Blocked = LoadBlockedNames();

    /// <summary>
    /// Prüft und vereinheitlicht einen Namen: nur A–Z, 3–16 Zeichen, erster Buchstabe groß, Rest klein
    /// („tObIaS“ → „Tobias“). Bei einem Fehler ist <paramref name="name"/> leer.
    /// </summary>
    public static PlayerNameError TryNormalize(string? input, out string name)
    {
        name = "";
        var trimmed = input?.Trim() ?? "";
        if (trimmed.Length is < MinLength or > MaxLength)
        {
            return PlayerNameError.Length;
        }

        if (!LettersOnly().IsMatch(trimmed))
        {
            return PlayerNameError.Characters;
        }

        var normalized = char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
        if (Blocked.Contains(normalized))
        {
            return PlayerNameError.Blocked;
        }

        name = normalized;
        return PlayerNameError.None;
    }

    public static string Describe(PlayerNameError error) => error switch
    {
#pragma warning disable CA1863 // Vorlage kommt je Sprache aus der Ressource; Zwischenspeichern lohnt nicht.
        PlayerNameError.Length => string.Format(CultureInfo.CurrentCulture, Text("PlayerName.Length"), MinLength, MaxLength),
#pragma warning restore CA1863
        PlayerNameError.Characters => Text("PlayerName.Characters"),
        PlayerNameError.Blocked => Text("PlayerName.Blocked"),
        _ => "",
    };

    private static string Text(string key) => Texts.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;

    [GeneratedRegex("^[A-Za-z]+$")]
    private static partial Regex LettersOnly();

    /// <summary>Sperrliste als eingebettete Ressource: ein Name pro Zeile, # leitet Kommentare ein.</summary>
    private static HashSet<string> LoadBlockedNames()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("BrowserGame.Server.Accounts.blocked-names.txt")
            ?? throw new InvalidOperationException("Sperrliste blocked-names.txt fehlt.");
        using var reader = new StreamReader(stream);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var entry = line.Split('#', 2)[0].Trim();
            if (entry.Length > 0)
            {
                names.Add(entry);
            }
        }

        return names;
    }
}
