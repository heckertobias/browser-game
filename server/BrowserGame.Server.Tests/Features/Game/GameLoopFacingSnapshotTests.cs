using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public sealed class GameLoopFacingSnapshotTests
{
    private const float DefaultFacing = (float)GameSettings.DefaultFacing;
    private const float FacingLeft = (float)Math.PI;
    private const float FacingUp = (float)(-Math.PI / 2);

    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static Snapshot LastSnapshot(PlayerConnection connection) =>
        Drain(connection).Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;

    private static float FacingOf(Snapshot snapshot, string id) => snapshot.Players.Single(p => p.Id == id).Facing;

    [Fact] // AK-12, Constitution §8
    public void Welcome_carries_min_facing_input()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();

        var welcome = Drain(a).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome;

        Assert.Equal((float)GameSettings.MinFacingInput, welcome.MinFacingInput);
    }

    [Fact] // AK-4
    public void New_figure_has_default_facing_in_snapshot()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();

        Assert.Equal(DefaultFacing, FacingOf(LastSnapshot(a), "acc"));
    }

    [Fact] // AK-8
    public void Snapshot_carries_facing_of_every_player()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        var b = game.Join("b", "B", null);
        game.Tick();
        a.Inbox.Writer.TryWrite(new MoveInput(1, -1, 0));
        game.Tick();

        var seenByB = LastSnapshot(b);
        var seenByA = LastSnapshot(a);

        Assert.Equal(FacingLeft, FacingOf(seenByB, "a"));
        Assert.Equal(DefaultFacing, FacingOf(seenByB, "b"));
        Assert.Equal(FacingLeft, FacingOf(seenByA, "a"));
        Assert.Equal(DefaultFacing, FacingOf(seenByA, "b"));
    }

    [Fact] // AK-8
    public void Waiting_figure_keeps_its_facing_in_snapshot()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        var b = game.Join("b", "B", null);
        game.Tick();
        a.Inbox.Writer.TryWrite(new MoveInput(1, -1, 0));
        game.Tick();

        game.Leave(a);
        game.Tick();

        var entry = LastSnapshot(b).Players.Single(p => p.Id == "a");
        Assert.True(entry.Away);
        Assert.Equal(FacingLeft, entry.Facing);
    }

    [Fact] // AK-12
    public void Facing_follows_movement_and_stays_when_input_stops()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();
        a.Inbox.Writer.TryWrite(new MoveInput(1, 0, -1));
        game.Tick();
        Assert.Equal(FacingUp, FacingOf(LastSnapshot(a), "acc"));

        for (var i = 0; i < World.MaxCoastTicks + 2; i++)
        {
            Assert.Empty(game.Tick());
        }

        Assert.Equal(FacingUp, FacingOf(LastSnapshot(a), "acc"));
    }

    [Fact] // AK-12
    public void Input_contract_has_no_facing()
    {
        Assert.DoesNotContain(
            BrowserGame.Contracts.V1.Input.Descriptor.Fields.InDeclarationOrder(),
            f => f.Name.Contains("facing", StringComparison.Ordinal));
    }
}
