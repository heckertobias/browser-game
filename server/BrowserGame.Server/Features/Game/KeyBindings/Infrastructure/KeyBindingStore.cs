using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Schreibt die Tastenbelegung eines Accounts (002 AK-9, AK-10). Sofort und einzeln, ohne Warteschlange: Wer sich danach
/// auf einem anderen Gerät anmeldet, liest schon den neuen Stand. Kein Hintergrunddienst, Speichern ist selten.
/// </summary>
internal sealed class KeyBindingStore(IServiceScopeFactory scopes)
{
    /// <summary>
    /// <paramref name="stored"/> aus <see cref="KeyBindingRules.ToStored"/>; <c>null</c> = Standard. Einen Account, den es nicht
    /// (mehr) gibt, trifft das Update nicht; es wird nichts angelegt.
    /// </summary>
    public async Task SaveAsync(string accountId, byte[]? stored, CancellationToken cancellationToken)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        await db.Users.Where(a => a.Id == accountId)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.KeyBindings, stored), cancellationToken);
    }
}
