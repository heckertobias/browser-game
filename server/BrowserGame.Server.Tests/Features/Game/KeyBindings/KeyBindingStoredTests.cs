using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using ProtoAction = BrowserGame.Contracts.V1.Action;

namespace BrowserGame.Server.Tests;

public sealed class KeyBindingStoredTests
{
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

    [Fact] // AK-10
    public void FromStored_null_is_empty() =>
        Assert.Equal(new KeyBindings(), KeyBindingRules.FromStored(null));

    [Fact] // AK-9
    public void FromStored_reads_what_ToStored_wrote() =>
        Assert.Equal(Custom(), KeyBindingRules.FromStored(KeyBindingRules.ToStored(Custom())));

    [Fact] // AK-9
    public void FromStored_corrupt_bytes_are_empty() =>
        Assert.Equal(new KeyBindings(), KeyBindingRules.FromStored([0x0A, 0x05]));
}
