using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CommandRegistryTests
{
    private static Command Noop(int parameterCount = 0) => new(parameterCount, static (_, _) => { });

    [Fact] // AK-10, AK-3
    public void Knows_the_built_in_commands_without_configuration()
    {
        var registry = new CommandRegistry();

        Assert.True(registry.TryGet("fullHealth", out var fullHealth));
        Assert.Equal(0, fullHealth.ParameterCount);
        Assert.True(registry.TryGet("resetPosition", out var resetPosition));
        Assert.Equal(0, resetPosition.ParameterCount);
    }

    [Theory] // AK-3
    [InlineData("FULLHEALTH")]
    [InlineData("fullhealth")]
    [InlineData("ResetPosition")]
    public void Lookup_ignores_case(string name)
    {
        Assert.True(new CommandRegistry().TryGet(name, out _));
    }

    [Fact] // AK-4
    public void Unknown_name_is_not_found()
    {
        Assert.False(new CommandRegistry().TryGet("nope", out var command));
        Assert.Null(command);
    }

    [Fact] // AK-3
    public void Registered_command_can_be_found()
    {
        var registry = new CommandRegistry();
        var command = Noop();

        registry.Register("spawnThing", command);

        Assert.True(registry.TryGet("spawnThing", out var found));
        Assert.Same(command, found);
    }

    [Fact] // AK-3
    public void Register_rejects_a_known_name_in_any_case()
    {
        var registry = new CommandRegistry();

        Assert.Throws<ArgumentException>(() => registry.Register("FullHealth", Noop()));
    }
}
