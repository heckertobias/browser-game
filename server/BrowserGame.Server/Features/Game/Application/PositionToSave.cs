namespace BrowserGame.Server.Features.Game;

/// <summary>Position einer Figur zum Speichern; im Tick-Thread kopiert, damit spätere Bewegung sie nicht ändert.</summary>
internal readonly record struct PositionToSave(string AccountId, double X, double Y);
