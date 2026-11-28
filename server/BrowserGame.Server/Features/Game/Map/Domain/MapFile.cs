using System.Text.Json;
using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Liest eine Kartendatei (018, Format 1) und prüft sie. Ohne Datei, Netz und DB; das Lesen der Datei macht der Aufrufer.
/// </summary>
internal static class MapFile
{
    public const int Format = 1;

    private static readonly string[] RootFields = ["format", "name", "tileSize", "origin", "legend", "rows"];
    private static readonly string[] OriginFields = ["x", "y"];

    private static readonly Dictionary<string, GroundKind> Kinds = new(StringComparer.Ordinal)
    {
        ["grass"] = GroundKind.Grass,
        ["path"] = GroundKind.Path,
        ["water"] = GroundKind.Water,
        ["mountain"] = GroundKind.Mountain,
    };

    public static AreaMap Parse(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException e)
        {
            throw new MapFormatException((e.LineNumber ?? 0) + 1, e);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Error("$", "muss ein JSON-Objekt sein");
            }

            var fields = Fields(root, "$");

            if (!fields.TryGetValue("format", out var format))
            {
                throw Error("$.format", "fehlt");
            }

            var formatNumber = Integer(format, "$.format");
            if (formatNumber != Format)
            {
                throw Error("$.format", $"Format {formatNumber} wird nicht unterstützt, erwartet {Format}");
            }

            RejectUnknown(fields, "$", RootFields);
            foreach (var name in RootFields)
            {
                Required(fields, "$", name);
            }

            if (fields["name"].ValueKind != JsonValueKind.String)
            {
                throw Error("$.name", "muss ein Text sein");
            }

            var tileSize = Integer(fields["tileSize"], "$.tileSize");
            if (tileSize <= 0)
            {
                throw Error("$.tileSize", "muss größer als 0 sein");
            }

            var legend = ReadLegend(fields["legend"]);
            var (width, height, ground) = ReadRows(fields["rows"], legend);
            var (originX, originY) = ReadOrigin(fields["origin"], width, height);

            return new AreaMap(tileSize, width, height, -originX, -originY, ground);
        }
    }

    private static Dictionary<char, GroundKind> ReadLegend(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Error("$.legend", "muss ein JSON-Objekt sein");
        }

        var legend = new Dictionary<char, GroundKind>();
        foreach (var entry in element.EnumerateObject())
        {
            var key = NameOf(entry, "$.legend");
            var path = $"$.legend['{key}']";
            if (key.Length != 1)
            {
                throw Error(path, "Schlüssel muss genau ein Zeichen sein");
            }

            if (entry.Value.ValueKind != JsonValueKind.String
                || !Kinds.TryGetValue(Text(entry.Value, path), out var kind))
            {
                throw Error(path, $"unbekannte Bodenart, erlaubt: {string.Join(", ", Kinds.Keys)}");
            }

            if (!legend.TryAdd(key[0], kind))
            {
                throw Error(path, "Zeichen doppelt");
            }
        }

        return legend;
    }

    private static (int Width, int Height, GroundKind[] Ground) ReadRows(
        JsonElement element, Dictionary<char, GroundKind> legend)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw Error("$.rows", "muss eine Liste sein");
        }

        var height = element.GetArrayLength();
        if (height == 0)
        {
            throw Error("$.rows", "Karte ist leer");
        }

        var rows = new string[height];
        var r = 0;
        foreach (var row in element.EnumerateArray())
        {
            if (row.ValueKind != JsonValueKind.String)
            {
                throw Error($"$.rows[{r}]", "muss ein Text sein");
            }

            rows[r] = Text(row, $"$.rows[{r}]");
            r++;
        }

        var width = rows[0].Length;
        if (width == 0)
        {
            throw Error("$.rows[0]", "Karte ist leer");
        }

        var ground = new GroundKind[width * height];
        for (r = 0; r < height; r++)
        {
            if (rows[r].Length != width)
            {
                throw Error($"$.rows[{r}]", $"Zeile hat {rows[r].Length} Zeichen, erwartet {width}");
            }

            for (var c = 0; c < width; c++)
            {
                if (!legend.TryGetValue(rows[r][c], out var kind))
                {
                    throw Error($"$.rows[{r}][{c}]", $"unbekanntes Zeichen '{rows[r][c]}'");
                }

                ground[(r * width) + c] = kind;
            }
        }

        return (width, height, ground);
    }

    private static (int X, int Y) ReadOrigin(JsonElement element, int width, int height)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw Error("$.origin", "muss ein JSON-Objekt sein");
        }

        var fields = Fields(element, "$.origin");
        RejectUnknown(fields, "$.origin", OriginFields);
        var x = Integer(Required(fields, "$.origin", "x"), "$.origin.x");
        var y = Integer(Required(fields, "$.origin", "y"), "$.origin.y");
        if (x < 0 || x >= width)
        {
            throw Error("$.origin.x", "liegt außerhalb der Karte");
        }

        if (y < 0 || y >= height)
        {
            throw Error("$.origin.y", "liegt außerhalb der Karte");
        }

        return (x, y);
    }

    private static Dictionary<string, JsonElement> Fields(JsonElement obj, string path)
    {
        var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in obj.EnumerateObject())
        {
            var name = NameOf(property, path);
            if (!fields.TryAdd(name, property.Value))
            {
                throw Error($"{path}.{name}", "Feld doppelt");
            }
        }

        return fields;
    }

    private static void RejectUnknown(Dictionary<string, JsonElement> fields, string path, string[] allowed)
    {
        foreach (var name in fields.Keys)
        {
            if (Array.IndexOf(allowed, name) < 0)
            {
                throw Error($"{path}.{name}", "unbekanntes Feld");
            }
        }
    }

    private static JsonElement Required(Dictionary<string, JsonElement> fields, string path, string name) =>
        fields.TryGetValue(name, out var value) ? value : throw Error($"{path}.{name}", "fehlt");

    private static int Integer(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var number))
        {
            throw Error(path, "muss eine ganze Zahl sein");
        }

        return number;
    }

    // Ein einzelner Surrogat (\ud800) ist gültiges JSON, aber kein gültiger Text.
    private static string Text(JsonElement value, string path)
    {
        try
        {
            return value.GetString()!;
        }
        catch (InvalidOperationException)
        {
            throw Error(path, "enthält ungültige Unicode-Zeichen");
        }
    }

    private static string NameOf(JsonProperty property, string objectPath)
    {
        try
        {
            return property.Name;
        }
        catch (InvalidOperationException)
        {
            throw Error(objectPath, "ein Feldname enthält ungültige Unicode-Zeichen");
        }
    }

    private static MapFormatException Error(string location, string reason) => new(location, reason);
}
