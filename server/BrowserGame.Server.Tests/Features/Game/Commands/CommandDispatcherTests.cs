using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CommandDispatcherTests
{
    private const double MaxHp = 80;
    private const double MaxMana = 40;
    private const double Far = 500; // weit außerhalb des SpawnRadius

    private static World NewWorld() => new(speed: 100, tickRateHz: 20, new Random(1), MaxHp, MaxMana);

    private static (World World, Player Self, CommandContext Context) Setup()
    {
        var world = NewWorld();
        var self = world.AddPlayer("a", "A", 0, Far, Far);
        world.SetVitals("a", 10, 0);
        return (world, self, new CommandContext(world, self, 0));
    }

    private static CommandRejected.Types.Reason? Execute(string text, CommandContext context) =>
        new CommandDispatcher(new CommandRegistry()).Execute(text, context);

    private static (double, double, double, double, uint) State(Player p) => (p.X, p.Y, p.Hp, p.Mana, p.SpawnSeq);

    private static double DistanceFromOrigin(Player player) => Math.Sqrt(player.X * player.X + player.Y * player.Y);

    [Fact] // AK-6
    public void FullHealth_fills_hp_and_mana()
    {
        var (_, self, context) = Setup();

        Assert.Null(Execute("/run fullHealth()", context));

        Assert.Equal(MaxHp, self.Hp);
        Assert.Equal(MaxMana, self.Mana);
        Assert.Equal((Far, Far), (self.X, self.Y));
        Assert.Equal(0u, self.SpawnSeq);
    }

    [Theory] // AK-6, AK-3
    [InlineData("/run fullHealth")]
    [InlineData("/run  fullHealth ( )")]
    [InlineData("/Run FULLHEALTH()")]
    [InlineData("/run fullhealth()")]
    public void FullHealth_runs_in_every_valid_form(string text)
    {
        var (_, self, context) = Setup();

        Assert.Null(Execute(text, context));

        Assert.Equal(MaxHp, self.Hp);
        Assert.Equal(MaxMana, self.Mana);
    }

    [Fact] // AK-7
    public void ResetPosition_moves_to_start_and_keeps_vitals()
    {
        var (_, self, context) = Setup();

        Assert.Null(Execute("/run resetPosition()", context));

        Assert.True(DistanceFromOrigin(self) <= World.SpawnRadius);
        Assert.Equal(10, self.Hp);
        Assert.Equal(0, self.Mana);
        Assert.Equal(1u, self.SpawnSeq);
    }

    [Theory] // AK-4
    [InlineData("/xyz")]
    [InlineData("/run nope()")]
    [InlineData("/run nope(1)")]
    [InlineData("/run()")]
    [InlineData("/runfullHealth()")]
    public void Unknown_command_is_rejected_without_effect(string text)
    {
        var (_, self, context) = Setup();
        var before = State(self);

        Assert.Equal(CommandRejected.Types.Reason.UnknownCommand, Execute(text, context));

        Assert.Equal(before, State(self));
    }

    [Theory] // AK-4
    [InlineData("/run")]
    [InlineData("/run x(")]
    [InlineData("/run fullHealth(1)")]
    [InlineData("/run resetPosition(a, b)")]
    [InlineData("/run fullHealth(,)")]
    [InlineData("/run 1abc")]
    public void Invalid_command_is_rejected_without_effect(string text)
    {
        var (_, self, context) = Setup();
        var before = State(self);

        Assert.Equal(CommandRejected.Types.Reason.Invalid, Execute(text, context));

        Assert.Equal(before, State(self));
    }

    [Fact] // AK-11
    public void Commands_affect_only_the_caller()
    {
        var (world, self, context) = Setup();
        var other = world.AddPlayer("b", "B", 0, 10, 20);
        world.SetVitals("b", 30, 7);

        Assert.Null(Execute("/run fullHealth()", context));
        Assert.Null(Execute("/run resetPosition()", context));

        Assert.Equal((10, 20), (other.X, other.Y));
        Assert.Equal(30, other.Hp);
        Assert.Equal(7, other.Mana);
        Assert.Equal(0u, other.SpawnSeq);
        Assert.Equal(1u, self.SpawnSeq);
        Assert.Equal(MaxHp, self.Hp);
        Assert.Equal(MaxMana, self.Mana);
    }

    [Theory] // AK-9, AK-10
    [InlineData("/run fullHealth()")]
    [InlineData("/run resetPosition()")]
    public void Fresh_player_may_run_every_command(string text)
    {
        var world = NewWorld();
        var fresh = world.AddPlayer("new", "Neu", 0);

        Assert.Null(Execute(text, new CommandContext(world, fresh, 0)));
    }

    [Fact] // AK-9, AK-11
    public void Dispatch_takes_no_rights_or_target()
    {
        var contextParameters = typeof(CommandContext).GetConstructors().Single().GetParameters();
        Assert.Single(contextParameters, p => p.ParameterType == typeof(Player));
        Assert.DoesNotContain(contextParameters, p => p.ParameterType == typeof(string));

        var executeParameters = typeof(CommandDispatcher).GetMethod(nameof(CommandDispatcher.Execute))!.GetParameters();
        Assert.Equal([typeof(string), typeof(CommandContext)], executeParameters.Select(p => p.ParameterType));
    }

    [Fact] // AK-3
    public void Registered_command_gets_context_and_arguments()
    {
        var (_, _, context) = Setup();
        (CommandContext Context, IReadOnlyList<string> Args)? seen = null;
        var registry = new CommandRegistry();
        registry.Register("echo", new Command(2, (c, args) => seen = (c, args)));

        Assert.Null(new CommandDispatcher(registry).Execute("/run echo(A, B)", context));

        Assert.NotNull(seen);
        Assert.Same(context, seen.Value.Context);
        Assert.Equal(["A", "B"], seen.Value.Args);
    }

    [Fact] // AK-4
    public void Wrong_argument_count_does_not_run_the_command()
    {
        var (_, _, context) = Setup();
        var ran = false;
        var registry = new CommandRegistry();
        registry.Register("echo", new Command(2, (_, _) => ran = true));

        Assert.Equal(CommandRejected.Types.Reason.Invalid, new CommandDispatcher(registry).Execute("/run echo(A)", context));

        Assert.False(ran);
    }
}
