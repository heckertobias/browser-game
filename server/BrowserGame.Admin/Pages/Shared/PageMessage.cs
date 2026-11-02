namespace BrowserGame.Admin.Pages;

public enum MessageKind
{
    Success,
    Warning,
}

/// <summary>Meldung für das Partial <c>_Message</c> (005 AK-8). Ohne Text zeigt es nichts.</summary>
public sealed record PageMessage(MessageKind Kind, string? Text);
