using System.Resources;

namespace BrowserGame.Server.Features.Localization.Resources;

/// <summary>Markertyp für <c>Features/Localization/Resources/Texts(.de).resx</c>; <c>IStringLocalizer&lt;Texts&gt;</c> liefert die Texte (ADR 0008).</summary>
public sealed class Texts
{
    internal const string BaseName = "BrowserGame.Server.Features.Localization.Resources.Texts";

    // Statischer Zugriff für Code ohne DI (PlayerNames, später Mails).
    internal static ResourceManager ResourceManager { get; } = new(BaseName, typeof(Texts).Assembly);
}
