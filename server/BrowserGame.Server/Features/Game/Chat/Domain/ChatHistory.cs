namespace BrowserGame.Server.Features.Game;

/// <summary>Eine Nachricht im Chat-Verlauf. Unveränderlich, darf also ohne Kopie weitergegeben werden.</summary>
internal sealed record ChatHistoryEntry(string AccountId, string SenderName, string Text, DateTimeOffset SentAt);

/// <summary>
/// Die letzten <paramref name="capacity"/> Nachrichten in Eingangsreihenfolge (011 AK-15–17).
/// Eine Nachricht zählt nicht mehr, sobald sie genau <paramref name="maxAge"/> alt ist; das entspricht der Abfrage
/// im <c>ChatStore</c> (<c>SentAt &gt; now − maxAge</c>).
/// Wird ausschließlich vom Tick-Thread benutzt.
/// </summary>
internal sealed class ChatHistory(int capacity, TimeSpan maxAge)
{
    // Queue ist intern ein Ringpuffer; sie wächst nicht, weil Add vor dem Einfügen die älteste Nachricht entfernt.
    private readonly Queue<ChatHistoryEntry> _entries = new(capacity);

    public void Add(ChatHistoryEntry entry)
    {
        if (_entries.Count == capacity)
        {
            _entries.Dequeue();
        }

        _entries.Enqueue(entry);
    }

    /// <summary>Liefert eine Kopie der noch gültigen Nachrichten, älteste zuerst. Ändert den Verlauf nicht.</summary>
    public IReadOnlyList<ChatHistoryEntry> Recent(DateTimeOffset now)
    {
        var cutoff = now - maxAge;
        return _entries.Where(entry => entry.SentAt > cutoff).ToArray();
    }

    /// <summary>
    /// Entfernt alle Nachrichten des Accounts (011 AK-17); die Reihenfolge der übrigen bleibt.
    /// Früher verdrängte Nachrichten kommen nicht zurück: Der Verlauf hat danach weniger Einträge, bis neue nachrücken.
    /// </summary>
    public void RemoveAccount(string accountId)
    {
        var kept = _entries.Where(entry => entry.AccountId != accountId).ToArray();
        _entries.Clear();
        foreach (var entry in kept)
        {
            _entries.Enqueue(entry);
        }
    }
}
