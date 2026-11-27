using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

/// <summary>017 T3: Command toggleFacingArrows ohne Netz und DB.</summary>
public sealed class ToggleFacingArrowsCommandTests
{
    private static World NewWorld() => new(speed: 100, tickRateHz: 20, new Random(1), maxHp: 80, maxMana: 40);

    private static (World World, Player Self, Player Other, List<ServerMessage> Replies, CommandContext Context) Setup()
    {
        var world = NewWorld();
        var self = world.AddPlayer("a", "A", 0, 500, 500);
        var other = world.AddPlayer("b", "B", 1, 0, 0);
        world.SetVitals("a", 10, 5);
        world.SetVitals("b", 20, 7);
        world.EnqueueInput("b", new MoveInput(1, 1, 0));
        world.Step();
        List<ServerMessage> replies = [];
        return (world, self, other, replies, new CommandContext(world, self, 0) { Reply = replies.Add });
    }

    private static CommandRejected.Types.Reason? Execute(string text, CommandContext context) =>
        new CommandDispatcher(new CommandRegistry()).Execute(text, context);

    private static List<(string, double, double, double, double, uint, object?, object?)> State(World world) =>
        [.. world.Players.OrderBy(p => p.Id).Select(p => (p.Id, p.X, p.Y, p.Hp, p.Mana, p.SpawnSeq, (object?)p.Facing, (object?)p.Away))];

    [Fact] // AK-11
    public void Registry_knows_toggleFacingArrows_without_parameters()
    {
        Assert.True(new CommandRegistry().TryGet("toggleFacingArrows", out var command));
        Assert.Equal(0, command.ParameterCount);
    }

    [Fact] // AK-11
    public void Toggle_replies_exactly_once_with_ToggleFacingArrows()
    {
        var (_, _, _, replies, context) = Setup();

        Assert.Null(Execute("/run toggleFacingArrows()", context));

        Assert.Equal(ServerMessage.PayloadOneofCase.ToggleFacingArrows, Assert.Single(replies).PayloadCase);
    }

    [Theory] // AK-11
    [InlineData("/run toggleFacingArrows")]
    [InlineData("/Run TOGGLEFACINGARROWS()")]
    [InlineData("/run  togglefacingarrows ( )")]
    public void Toggle_runs_in_every_valid_form(string text)
    {
        var (_, _, _, replies, context) = Setup();

        Assert.Null(Execute(text, context));

        Assert.Equal(ServerMessage.PayloadOneofCase.ToggleFacingArrows, Assert.Single(replies).PayloadCase);
    }

    [Fact] // AK-12
    public void Toggle_leaves_the_world_unchanged()
    {
        var (world, _, _, _, context) = Setup();
        var before = State(world);

        Execute("/run toggleFacingArrows()", context);

        Assert.Equal(before, State(world));
    }

    [Fact] // AK-11, AK-13
    public void Toggle_twice_replies_twice()
    {
        var (_, _, _, replies, context) = Setup();

        Execute("/run toggleFacingArrows()", context);
        Execute("/run toggleFacingArrows()", context);

        Assert.Equal(2, replies.Count);
        Assert.All(replies, r => Assert.Equal(ServerMessage.PayloadOneofCase.ToggleFacingArrows, r.PayloadCase));
    }

    [Fact] // AK-11
    public void Toggle_with_an_argument_is_rejected_without_reply()
    {
        var (_, _, _, replies, context) = Setup();

        Assert.Equal(CommandRejected.Types.Reason.Invalid, Execute("/run toggleFacingArrows(1)", context));

        Assert.Empty(replies);
    }

    [Fact]
    public void Default_reply_does_nothing()
    {
        var (world, self, _, _, _) = Setup();
        var before = State(world);

        Assert.Null(Execute("/run toggleFacingArrows()", new CommandContext(world, self, 0)));

        Assert.Equal(before, State(world));
    }
}
