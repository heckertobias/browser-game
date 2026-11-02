using System.Globalization;
using BrowserGame.Admin.Resources;

namespace BrowserGame.Admin.Tests;

/// <summary>Erwartete Texte aus AdminTexts (neutral = Englisch) statt Literalen (005 plan.md „Texte in Tests“).</summary>
internal static class ExpectedText
{
    public static string Get(string key) =>
        AdminTexts.ResourceManager.GetString(key, CultureInfo.InvariantCulture) ?? throw new KeyNotFoundException(key);

#pragma warning disable CA1863 // Vorlage kommt aus der Ressource; Zwischenspeichern lohnt nicht.
    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.InvariantCulture, Get(key), args);
#pragma warning restore CA1863
}
