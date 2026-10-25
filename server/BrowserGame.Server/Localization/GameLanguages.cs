namespace BrowserGame.Server.Localization;

/// <summary>Unterstützte Sprachen (006, ADR 0008). Neue Sprache = Eintrag hier + eigene .resx.</summary>
internal static class GameLanguages
{
    public const string Default = "en";

    public static IReadOnlyList<string> Supported { get; } = [Default, "de"];

    /// <summary>Unterstützter Code oder <see cref="Default"/> – für Cookie, Formular und Datenbank.</summary>
    public static string Normalize(string? code) =>
        Supported.FirstOrDefault(s => string.Equals(s, code?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? Default;
}
