using BrowserGame.Server.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>003 T14: Ohne Konfiguration gelten die Werte der Spec – 20 s Nachlauf (AK-20), Speichern alle 5 s (AK-19).</summary>
public class GameDefaultsTests
{
    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());

    [Fact] // AK-20, AK-23
    public void Disconnected_figure_waits_20_seconds_by_default()
    {
        using var game = NewGame();
        var connection = game.Join("acc", "A", (3, 4));
        game.Tick();
        game.Leave(connection);

        for (var i = 0; i < 20 * GameSettings.TickRateHz; i++)
        {
            Assert.Empty(game.Tick());
        }

        var removed = Assert.Single(game.Tick());
        Assert.Equal("acc", removed.Id);
        Assert.Equal((3d, 4d), (removed.X, removed.Y));
    }

    [Fact] // AK-19
    public void Positions_are_queued_every_5_seconds_by_default()
    {
        using var game = NewGame();
        game.Join("acc", "A", (1, 2));
        var interval = 5 * GameSettings.TickRateHz;

        for (var i = 1; i < interval; i++)
        {
            game.TickAndQueuePositions();
        }

        Assert.False(game.PositionBatches.TryRead(out _));

        game.TickAndQueuePositions();

        Assert.True(game.PositionBatches.TryRead(out var batch));
        Assert.Equal(new PositionToSave("acc", 1, 2), Assert.Single(batch));
        Assert.False(game.PositionBatches.TryRead(out _));
    }
}
