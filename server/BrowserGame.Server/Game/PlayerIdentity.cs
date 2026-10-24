namespace BrowserGame.Server.Game;

/// <summary>Zufällige Namen und Farben für anonyme Spieler.</summary>
internal static class PlayerIdentity
{
    private static readonly string[] Prefixes =
        ["Al", "Bel", "Cor", "Dra", "El", "Fen", "Gar", "Hal", "Ior", "Kel", "Lor", "Mor", "Nim", "Or", "Pel", "Ryn", "Sar", "Thal", "Ul", "Vor"];

    private static readonly string[] Suffixes =
        ["adan", "andor", "ric", "wyn", "eth", "ion", "mir", "dris", "gorn", "wen", "thas", "ra"];

    private static readonly uint[] Colors =
        [0xE6194B, 0x3CB44B, 0xFFE119, 0x4363D8, 0xF58231, 0x911EB4, 0x46F0F0, 0xF032E6, 0xBCF60C, 0xFABEBE, 0x008080, 0xE6BEFF];

    public static string NewId() => Guid.NewGuid().ToString("N")[..12];

    public static string RandomName(Random random) =>
        Prefixes[random.Next(Prefixes.Length)] + Suffixes[random.Next(Suffixes.Length)];

    public static uint RandomColor(Random random) => Colors[random.Next(Colors.Length)];
}
