using System.Collections.Immutable;
using BrowserGame.Contracts.V1;
using Google.Protobuf;
using ProtoAction = BrowserGame.Contracts.V1.Action;

namespace BrowserGame.Server.Features.Game;

/// <summary>Prüft eine Tastenbelegung (002), bevor der Server sie speichert. Ohne Netz und DB.</summary>
internal static class KeyBindingRules
{
    public const int MaxCodesPerAction = 2;
    public const int MaxCodeLength = 32;

    /// <summary>Esc öffnet das Menü, Enter (beide Tasten) den Chat (AK-8).</summary>
    public static ImmutableArray<string> ReservedCodes { get; } = ["Escape", "Enter", "NumpadEnter"];

    private static readonly ProtoAction[] MoveActions =
        [ProtoAction.MoveUp, ProtoAction.MoveDown, ProtoAction.MoveLeft, ProtoAction.MoveRight];

    private static readonly MessageParser<KeyBindings> KnownFieldsParser = KeyBindings.Parser.WithDiscardUnknownFields(true);

    /// <summary>
    /// Leer ist gültig (Standard des Clients). Sonst: jede Aktion bekannt und höchstens einmal, je Aktion 0–2 Codes,
    /// jede Bewegungsaktion vorhanden mit mindestens einem Code (AK-11), kein Code doppelt (AK-7), keiner reserviert (AK-8),
    /// Codes 1–32 Zeichen aus [A-Za-z0-9].
    /// </summary>
    public static bool IsValid(KeyBindings bindings)
    {
        if (bindings.Bindings.Count == 0)
        {
            return true;
        }

        var actions = new HashSet<ProtoAction>();
        var codes = new HashSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings.Bindings)
        {
            if (binding.Action == ProtoAction.Unspecified || !Enum.IsDefined(binding.Action) || !actions.Add(binding.Action))
            {
                return false;
            }

            if (binding.Codes.Count > MaxCodesPerAction)
            {
                return false;
            }

            if (binding.Codes.Count == 0 && MoveActions.Contains(binding.Action))
            {
                return false;
            }

            foreach (var code in binding.Codes)
            {
                if (!IsWellFormed(code) || ReservedCodes.Contains(code) || !codes.Add(code))
                {
                    return false;
                }
            }
        }

        return MoveActions.All(actions.Contains);
    }

    /// <summary>Bytes für <c>Account.KeyBindings</c>: leer → <c>null</c> (Standard), sonst nur die bekannten Felder.</summary>
    public static byte[]? ToStored(KeyBindings bindings) =>
        bindings.Bindings.Count == 0 ? null : KnownFieldsParser.ParseFrom(bindings.ToByteArray()).ToByteArray();

    private static bool IsWellFormed(string code) =>
        code.Length is >= 1 and <= MaxCodeLength && code.All(char.IsAsciiLetterOrDigit);
}
