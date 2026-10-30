using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public class GameLoopPositionsTests
{
    private static GameLoop NewGame(string saveIntervalSeconds, string lingerSeconds = "60") => new(
        NullLogger<GameLoop>.Instance,
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [GameSettings.PositionSaveIntervalSecondsKey] = saveIntervalSeconds,
            [GameSettings.LingerSecondsKey] = lingerSeconds,
        }).Build());

    private static List<IReadOnlyList<PositionToSave>> Batches(GameLoop game)
    {
        var batches = new List<IReadOnlyList<PositionToSave>>();
        while (game.PositionBatches.TryRead(out var batch))
        {
            batches.Add(batch);
        }

        return batches;
    }

    private static void TickTimes(GameLoop game, int count)
    {
        for (var i = 0; i < count; i++)
        {
            game.TickAndQueuePositions();
        }
    }

    [Fact] // AK-19
    public void All_figures_are_queued_every_interval()
    {
        using var game = NewGame("1");
        game.Join("a", "A", (100, 50));
        game.Join("b", "B", (-20, 30));

        TickTimes(game, 19);
        Assert.Empty(Batches(game));

        TickTimes(game, 1);
        var batch = Assert.Single(Batches(game));
        Assert.Equivalent(new[] { new PositionToSave("a", 100, 50), new PositionToSave("b", -20, 30) }, batch);

        TickTimes(game, 19);
        Assert.Empty(Batches(game));

        TickTimes(game, 1);
        Assert.Single(Batches(game));
    }

    [Fact] // AK-19
    public void Waiting_figures_are_in_interval_batch()
    {
        using var game = NewGame("1");
        var a = game.Join("a", "A", (5, 6));
        game.TickAndQueuePositions();
        game.Leave(a);

        TickTimes(game, 19);

        var batch = Assert.Single(Batches(game));
        Assert.Equal(new PositionToSave("a", 5, 6), Assert.Single(batch));
    }

    [Fact] // AK-18, AK-23
    public void Expired_figure_is_queued_at_once()
    {
        using var game = NewGame("3600", "1");
        var a = game.Join("a", "A", (100, 50));
        game.TickAndQueuePositions();
        game.Leave(a);

        TickTimes(game, 20);
        Assert.Empty(Batches(game));

        game.TickAndQueuePositions();
        var batch = Assert.Single(Batches(game));
        Assert.Equal(new PositionToSave("a", 100, 50), Assert.Single(batch));
    }

    [Fact] // AK-18, AK-22, AK-16
    public void Signed_out_figure_is_queued_deleted_is_not()
    {
        using var game = NewGame("3600");
        game.Join("a", "A", (1, 2));
        game.Join("b", "B", (3, 4));
        game.TickAndQueuePositions();
        Batches(game);

        game.ForceLeave("a", savePosition: true);
        game.ForceLeave("b", savePosition: false);
        game.TickAndQueuePositions();

        var batch = Assert.Single(Batches(game));
        Assert.Equal(new PositionToSave("a", 1, 2), Assert.Single(batch));
    }

    [Fact] // AK-19
    public void Flush_queues_all_figures_and_completes()
    {
        using var game = NewGame("3600");
        game.Join("a", "A", (1, 2));
        var b = game.Join("b", "B", (3, 4));
        game.TickAndQueuePositions();
        game.Leave(b);
        game.TickAndQueuePositions();

        game.FlushPositions();

        var batch = Assert.Single(Batches(game));
        Assert.Equivalent(new[] { new PositionToSave("a", 1, 2), new PositionToSave("b", 3, 4) }, batch);
        Assert.True(game.PositionBatches.Completion.IsCompleted);
    }

    [Fact]
    public void Zero_interval_queues_every_tick()
    {
        using var game = NewGame("0");
        game.Join("a", "A", (1, 2));

        TickTimes(game, 3);

        Assert.Equal(3, Batches(game).Count);
    }
}
