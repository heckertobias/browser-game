using System.Resources;

namespace BrowserGame.Admin.Resources;

/// <summary>Markertyp für <c>Resources/AdminTexts.resx</c> (neutral = Englisch); <c>IStringLocalizer&lt;AdminTexts&gt;</c> liefert die Texte (ADR 0008).</summary>
public sealed class AdminTexts
{
    internal const string BaseName = "BrowserGame.Admin.Resources.AdminTexts";

    // Statischer Zugriff für Code ohne DI und für Tests.
    internal static ResourceManager ResourceManager { get; } = new(BaseName, typeof(AdminTexts).Assembly);
}
