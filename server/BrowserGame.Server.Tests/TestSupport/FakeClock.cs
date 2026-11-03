namespace BrowserGame.Server.Tests;

/// <summary>Uhr für Tests; läuft nur mit <see cref="Advance"/> weiter.</summary>
public sealed class FakeClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}
