using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Die gemeldete Abklingzeit entspricht den ganzen Cooldown-Ticks, nicht dem konfigurierten Wert (013 AK-11, AK-22):
/// Der Client soll genau so lange warten, wie der Server sperrt.
/// </summary>
public sealed class CombatCooldownTests
{
    private const double Tolerance = 1e-9;

    [Theory] // AK-11, AK-22 (Constitution §8)
    [InlineData(1, 20, 1)]
    [InlineData(0.99, 20, 1)]
    [InlineData(0.01, 20, 0.05)]
    [InlineData(0, 20, 0)]
    [InlineData(-1, 20, 0)]
    [InlineData(1, 30, 1)]
    [InlineData(0.01, 30, 1.0 / 30)]
    [InlineData(0.51, 30, 16.0 / 30)]
    public void Cooldown_seconds_are_whole_cooldown_ticks(double seconds, int tickRate, double expected)
    {
        var combat = new Combat(tickRate, globalCooldownSeconds: seconds);

        Assert.Equal(expected, combat.GlobalCooldownSeconds, Tolerance);
        Assert.Equal(combat.GlobalCooldownTicks, combat.GlobalCooldownSeconds * tickRate, Tolerance);
    }
}
