using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public sealed class GameLoopFacingTests
{
    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());

    [Fact] // AK-4, AK-11
    public void Signing_in_again_starts_with_default_facing()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", (0, 0));
        a.Inbox.Writer.TryWrite(new MoveInput(1, -1, 0));
        game.Tick();

        game.ForceLeave("acc", savePosition: true);
        var left = Assert.Single(game.Tick());
        Assert.Equal(Math.PI, left.Facing, 1e-9);

        game.Join("acc", "A", (left.X, left.Y));
        game.Tick();
        game.ForceLeave("acc", savePosition: true);
        var again = Assert.Single(game.Tick());

        Assert.Equal(GameSettings.DefaultFacing, again.Facing);
    }
}
