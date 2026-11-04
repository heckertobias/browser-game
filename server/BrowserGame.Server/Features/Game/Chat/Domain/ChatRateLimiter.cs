namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Bremse pro Account (011 AK-13): höchstens <paramref name="limit"/> angenommene Nachrichten im gleitenden Zeitfenster.
/// Eine Nachricht zählt nicht mehr, sobald sie genau <paramref name="window"/> alt ist. Abgelehnte zählen nie.
/// Wird ausschließlich vom Tick-Thread benutzt.
/// </summary>
internal sealed class ChatRateLimiter(int limit, TimeSpan window)
{
    private readonly Dictionary<string, Queue<DateTimeOffset>> _accepted = [];
    private DateTimeOffset _nextCleanup;

    /// <summary>Accounts im Speicher. Inaktive zählen mit, bis das nächste Aufräumen sie entfernt.</summary>
    public int TrackedAccounts => _accepted.Count;

    public bool TryAccept(string accountId, DateTimeOffset now)
    {
        if (now >= _nextCleanup)
        {
            RemoveInactiveAccounts(now);
            _nextCleanup = now + window;
        }

        if (!_accepted.TryGetValue(accountId, out var accepted))
        {
            accepted = new Queue<DateTimeOffset>(limit);
            _accepted.Add(accountId, accepted);
        }

        RemoveExpired(accepted, now);
        if (accepted.Count >= limit)
        {
            return false;
        }

        accepted.Enqueue(now);
        return true;
    }

    private void RemoveExpired(Queue<DateTimeOffset> accepted, DateTimeOffset now)
    {
        while (accepted.Count > 0 && now - accepted.Peek() >= window)
        {
            accepted.Dequeue();
        }
    }

    private void RemoveInactiveAccounts(DateTimeOffset now)
    {
        // Dictionary erlaubt Remove während foreach.
        foreach (var (accountId, accepted) in _accepted)
        {
            RemoveExpired(accepted, now);
            if (accepted.Count == 0)
            {
                _accepted.Remove(accountId);
            }
        }
    }
}
