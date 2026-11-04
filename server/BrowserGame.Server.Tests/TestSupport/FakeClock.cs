namespace BrowserGame.Server.Tests;

/// <summary>Uhr für Tests; läuft nur mit <see cref="Advance"/> weiter. Threadsicher: Im laufenden Server liest der Tick-Thread mit.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private readonly Lock _gate = new();
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow()
    {
        lock (_gate)
        {
            return _now;
        }
    }

    public void Advance(TimeSpan by)
    {
        lock (_gate)
        {
            _now += by;
        }
    }
}
