using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;
using ProtoAction = BrowserGame.Contracts.V1.Action;

namespace BrowserGame.Server.Tests;

public sealed class KeyBindingRulesTests
{
    private static readonly string[] ExpectedReservedCodes = ["Escape", "Enter", "NumpadEnter"];

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

    // W A S D; "Nach oben" bekommt die übergebenen Codes.
    private static KeyBindings WasdWithUp(params string[] upCodes) => Bindings(
        Bind(ProtoAction.MoveUp, upCodes),
        Bind(ProtoAction.MoveDown, "KeyS"),
        Bind(ProtoAction.MoveLeft, "KeyA"),
        Bind(ProtoAction.MoveRight, "KeyD"));

    private static KeyBindings Wasd() => WasdWithUp("KeyW");

    // Unbekanntes Feld 15 (Wire-Typ 2 = Länge + Bytes) mit `length` Null-Bytes; length < 128.
    private static byte[] UnknownField(int length) => [0x7A, (byte)length, .. new byte[length]];

    // AK-10
    [Fact]
    public void Empty_bindings_are_valid() =>
        Assert.True(KeyBindingRules.IsValid(new KeyBindings()));

    // AK-11
    [Fact]
    public void Wasd_is_valid() =>
        Assert.True(KeyBindingRules.IsValid(Wasd()));

    // AK-6, AK-7
    [Fact]
    public void Two_codes_per_action_are_valid() =>
        Assert.True(KeyBindingRules.IsValid(WasdWithUp("KeyW", "ArrowUp")));

    // AK-11
    [Fact]
    public void Order_of_actions_does_not_matter() =>
        Assert.True(KeyBindingRules.IsValid(Bindings(
            Bind(ProtoAction.MoveRight, "KeyD"),
            Bind(ProtoAction.MoveLeft, "KeyA"),
            Bind(ProtoAction.MoveDown, "KeyS"),
            Bind(ProtoAction.MoveUp, "KeyW"))));

    [Fact]
    public void Unspecified_action_is_invalid()
    {
        var bindings = Wasd();
        bindings.Bindings.Add(Bind(ProtoAction.Unspecified, "KeyQ"));
        Assert.False(KeyBindingRules.IsValid(bindings));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(99)]
    [InlineData(-1)]
    public void Unknown_action_is_invalid(int value)
    {
        var bindings = Wasd();
        bindings.Bindings.Add(Bind((ProtoAction)value, "KeyQ"));
        Assert.False(KeyBindingRules.IsValid(bindings));

        var parsed = KeyBindings.Parser.ParseFrom(bindings.ToByteArray());
        Assert.Equal((ProtoAction)value, parsed.Bindings[^1].Action);
        Assert.False(KeyBindingRules.IsValid(parsed));
    }

    // AK-7
    [Fact]
    public void Action_listed_twice_is_invalid()
    {
        var bindings = Wasd();
        bindings.Bindings.Add(Bind(ProtoAction.MoveUp, "ArrowUp"));
        Assert.False(KeyBindingRules.IsValid(bindings));
    }

    // AK-11
    [Fact]
    public void Missing_actions_are_valid()
    {
        Assert.True(KeyBindingRules.IsValid(Bindings(
            Bind(ProtoAction.MoveUp, "KeyW"),
            Bind(ProtoAction.MoveDown, "KeyS"),
            Bind(ProtoAction.MoveLeft, "KeyA"))));
        Assert.True(KeyBindingRules.IsValid(Bindings(Bind(ProtoAction.MoveUp, "KeyW"))));
    }

    // AK-11
    [Fact]
    public void Action_without_code_is_valid() =>
        Assert.True(KeyBindingRules.IsValid(WasdWithUp()));

    // AK-11
    [Fact]
    public void All_actions_without_code_are_valid_and_not_the_default()
    {
        var empty = Bindings(
            Bind(ProtoAction.MoveUp),
            Bind(ProtoAction.MoveDown),
            Bind(ProtoAction.MoveLeft),
            Bind(ProtoAction.MoveRight));

        Assert.True(KeyBindingRules.IsValid(empty));
        Assert.NotNull(KeyBindingRules.ToStored(empty));
    }

    // AK-7: Zustand nach dem Überschreiben – D liegt jetzt auf "Nach oben", "Nach rechts" ist leer.
    [Fact]
    public void Code_moved_to_other_action_is_valid() =>
        Assert.True(KeyBindingRules.IsValid(Bindings(
            Bind(ProtoAction.MoveUp, "KeyD"),
            Bind(ProtoAction.MoveDown, "KeyS"),
            Bind(ProtoAction.MoveLeft, "KeyA"),
            Bind(ProtoAction.MoveRight))));

    [Fact]
    public void Unknown_action_without_code_is_invalid()
    {
        Assert.False(KeyBindingRules.IsValid(Bindings(Bind(ProtoAction.Unspecified))));
        Assert.False(KeyBindingRules.IsValid(Bindings(Bind((ProtoAction)99))));
    }

    // 013: Angriffsaktionen sind belegbar.
    [Fact]
    public void Attack_actions_are_valid()
    {
        var bindings = Wasd();
        bindings.Bindings.Add(Bind(ProtoAction.AttackMelee, "Digit1"));
        bindings.Bindings.Add(Bind(ProtoAction.AttackRanged, "Digit2"));
        Assert.True(KeyBindingRules.IsValid(bindings));
    }

    // AK-6
    [Fact]
    public void More_than_two_codes_are_invalid() =>
        Assert.False(KeyBindingRules.IsValid(WasdWithUp("KeyW", "ArrowUp", "KeyI")));

    // AK-7
    [Fact]
    public void Same_code_twice_in_one_action_is_invalid() =>
        Assert.False(KeyBindingRules.IsValid(WasdWithUp("KeyW", "KeyW")));

    // AK-7
    [Fact]
    public void Same_code_in_two_actions_is_invalid() =>
        Assert.False(KeyBindingRules.IsValid(WasdWithUp("KeyW", "KeyS")));

    // AK-8
    [Theory]
    [InlineData("Escape")]
    [InlineData("Enter")]
    [InlineData("NumpadEnter")]
    public void Reserved_codes_are_invalid(string code)
    {
        Assert.False(KeyBindingRules.IsValid(WasdWithUp(code)));
        Assert.False(KeyBindingRules.IsValid(WasdWithUp("KeyW", code)));
    }

    // AK-8
    [Fact]
    public void Reserved_codes_are_escape_and_both_enter_keys() =>
        Assert.Equal(ExpectedReservedCodes, KeyBindingRules.ReservedCodes);

    // AK-5
    [Theory]
    [InlineData("ArrowUp")]
    [InlineData("Digit1")]
    [InlineData("Numpad0")]
    [InlineData("F12")]
    [InlineData("Space")]
    [InlineData("ShiftLeft")]
    [InlineData("IntlBackslash")]
    public void Real_key_codes_are_valid(string code) =>
        Assert.True(KeyBindingRules.IsValid(WasdWithUp(code)));

    [Theory]
    [InlineData("")]
    [InlineData("Key W")]
    [InlineData("Key-W")]
    [InlineData("Key_W")]
    [InlineData("KeyÄ")]
    [InlineData("Kéy")]
    [InlineData("KeyW\n")]
    [InlineData("Ｗ")]
    public void Malformed_codes_are_invalid(string code) =>
        Assert.False(KeyBindingRules.IsValid(WasdWithUp(code)));

    [Fact]
    public void Code_length_is_limited()
    {
        Assert.True(KeyBindingRules.IsValid(WasdWithUp(new string('A', KeyBindingRules.MaxCodeLength))));
        Assert.False(KeyBindingRules.IsValid(WasdWithUp(new string('A', KeyBindingRules.MaxCodeLength + 1))));
    }

    // AK-10
    [Fact]
    public void Empty_bindings_are_stored_as_null() =>
        Assert.Null(KeyBindingRules.ToStored(new KeyBindings()));

    // AK-9
    [Fact]
    public void Valid_bindings_are_stored_as_protobuf_bytes()
    {
        var stored = KeyBindingRules.ToStored(Wasd());

        Assert.Equal(Wasd().ToByteArray(), stored);
        Assert.Equal(Wasd(), KeyBindings.Parser.ParseFrom(stored));
    }

    [Fact]
    public void Unknown_fields_are_not_stored()
    {
        byte[] up = [.. Bind(ProtoAction.MoveUp, "KeyW").ToByteArray(), .. UnknownField(100)];
        byte[] bytes =
        [
            .. Bindings(Bind(ProtoAction.MoveDown, "KeyS"), Bind(ProtoAction.MoveLeft, "KeyA"), Bind(ProtoAction.MoveRight, "KeyD")).ToByteArray(),
            0x0A, (byte)up.Length, .. up,
            .. UnknownField(100),
        ];
        var parsed = KeyBindings.Parser.ParseFrom(bytes);
        var clean = Bindings(
            Bind(ProtoAction.MoveDown, "KeyS"),
            Bind(ProtoAction.MoveLeft, "KeyA"),
            Bind(ProtoAction.MoveRight, "KeyD"),
            Bind(ProtoAction.MoveUp, "KeyW"));

        Assert.True(parsed.ToByteArray().Length > clean.ToByteArray().Length);
        Assert.True(KeyBindingRules.IsValid(parsed));
        Assert.Equal(clean.ToByteArray(), KeyBindingRules.ToStored(parsed));
    }

    [Fact]
    public void Largest_valid_bindings_fit_in_the_column()
    {
        var actions = Enum.GetValues<ProtoAction>().Where(a => a != ProtoAction.Unspecified).ToList();
        KeyBinding[] all =
        [
            .. actions.Select((action, i) => Bind(
                action,
                new string((char)('A' + i), KeyBindingRules.MaxCodeLength - 1) + "a",
                new string((char)('A' + i), KeyBindingRules.MaxCodeLength - 1) + "b")),
        ];
        var largest = Bindings(all);

        Assert.True(KeyBindingRules.IsValid(largest));
        // Account.KeyBindings: HasMaxLength(512)
        Assert.InRange(KeyBindingRules.ToStored(largest)!.Length, 1, 512);
    }
}
