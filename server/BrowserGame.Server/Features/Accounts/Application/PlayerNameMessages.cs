using System.Globalization;
using BrowserGame.Server.Features.Localization.Resources;

namespace BrowserGame.Server.Features.Accounts;

/// <summary>Fehlertexte zu <see cref="PlayerNameError"/> in der aktuellen Sprache (006).</summary>
internal static class PlayerNameMessages
{
    public static string Describe(PlayerNameError error) => error switch
    {
#pragma warning disable CA1863 // Vorlage kommt je Sprache aus der Ressource; Zwischenspeichern lohnt nicht.
        PlayerNameError.Length => string.Format(CultureInfo.CurrentCulture, Text("PlayerName.Length"), PlayerNames.MinLength, PlayerNames.MaxLength),
#pragma warning restore CA1863
        PlayerNameError.Characters => Text("PlayerName.Characters"),
        PlayerNameError.Blocked => Text("PlayerName.Blocked"),
        _ => "",
    };

    private static string Text(string key) => Texts.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
}
