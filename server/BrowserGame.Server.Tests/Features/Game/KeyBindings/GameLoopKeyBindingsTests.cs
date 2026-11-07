using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using ProtoAction = BrowserGame.Contracts.V1.Action;

namespace BrowserGame.Server.Tests;

public sealed class GameLoopKeyBindingsTests
{
    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());

    private static KeyBinding Bind(ProtoAction action, params string[] codes)
    {
        var binding = new KeyBinding { Action = action };
        binding.Codes.AddRange(codes);
        return binding;
    }

    private static KeyBindings Bindings(params KeyBinding[] bindings)
    {
        var result = new KeyBindings();
        result.Bindings.AddRange(bindings);
        return result;
    }

    // Gültig und ungleich Standard: Pfeil hoch zusätzlich auf "Nach oben".
    private static KeyBindings Custom() => Bindings(
        Bind(ProtoAction.MoveUp, "KeyW", "ArrowUp"),
        Bind(ProtoAction.MoveDown, "KeyS"),
        Bind(ProtoAction.MoveLeft, "KeyA"),
        Bind(ProtoAction.MoveRight, "KeyD"));

    // Welcome ohne Feld 5 zählt wie leer.
    private static KeyBindings Of(Welcome welcome) => welcome.KeyBindings ?? new KeyBindings();

    private static Welcome WelcomeOf(PlayerConnection connection)
    {
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            var message = ServerMessage.Parser.ParseFrom(bytes);
            if (message.PayloadCase == ServerMessage.PayloadOneofCase.Welcome)
            {
                return message.Welcome;
            }
        }

        throw new Xunit.Sdk.XunitException("no Welcome");
    }

    [Fact] // AK-9
    public void Welcome_carries_key_bindings_of_connection()
    {
        using var game = NewGame();
        var connection = game.Join("acc", "A", null, keyBindings: Custom());
        game.Tick();

        Assert.Equal(Custom(), Of(WelcomeOf(connection)));
    }

    [Fact] // AK-10
    public void Welcome_without_key_bindings_is_empty()
    {
        using var game = NewGame();
        var connection = game.Join("acc", "A", null);
        game.Tick();

        Assert.Equal(new KeyBindings(), Of(WelcomeOf(connection)));
    }

    [Fact] // AK-9
    public void Take_over_sends_key_bindings_of_new_connection()
    {
        using var game = NewGame();
        game.Join("acc", "A", null, keyBindings: new KeyBindings());
        game.Tick();
        var second = game.Join("acc", "A", null, keyBindings: Custom());
        game.Tick();

        Assert.Equal(Custom(), Of(WelcomeOf(second)));
    }
}
