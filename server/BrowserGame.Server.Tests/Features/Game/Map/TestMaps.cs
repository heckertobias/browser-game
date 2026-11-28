namespace BrowserGame.Server.Tests;

/// <summary>Kartendateien für Tests von MapLoader und Server (018): die echte Wiese und kleine Beispiel-Dateien.</summary>
internal static class TestMaps
{
    public static readonly string MeadowPath = Path.Combine(AppContext.BaseDirectory, "Maps", "meadow.json");

    // 4 × 3, origin (1, 2), wie in MapFileTests.
    public const string Small = """{"format":1,"name":"test","tileSize":16,"origin":{"x":1,"y":2},"legend":{".":"grass","=":"path","~":"water","^":"mountain"},"rows":["^^^^","^.=^","^~.^"]}""";

    // Bodenarten von Small im Welcome, eine GroundKind je Byte (1 Gras, 2 Weg, 3 Wasser, 4 Berg).
    public static readonly byte[] SmallGround = [4, 4, 4, 4, 4, 1, 2, 4, 4, 3, 1, 4];

    // Wie Small, aber mit unbekanntem Zeichen 'x' in Zeile 1, Spalte 2.
    public const string Invalid = """{"format":1,"name":"test","tileSize":16,"origin":{"x":1,"y":2},"legend":{".":"grass","=":"path","~":"water","^":"mountain"},"rows":["^^^^","^.x^","^~.^"]}""";

    public const string InvalidLocation = "$.rows[1][2]";
}
