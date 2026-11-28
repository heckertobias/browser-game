using System.Text.Json;
using System.Text.Json.Nodes;
using BrowserGame.Server.Features.Game;
using GroundKind = BrowserGame.Contracts.V1.GroundKind;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Kartendatei lesen und prüfen (018 AK-6): eine gültige Datei ergibt die Karte; jeder Fehler nennt seine Fundstelle.
/// </summary>
public sealed class MapFileTests
{
    // 4 x 3; origin (1, 2).
    private static JsonObject Valid() => new()
    {
        ["format"] = 1,
        ["name"] = "test",
        ["tileSize"] = 16,
        ["origin"] = new JsonObject { ["x"] = 1, ["y"] = 2 },
        ["legend"] = new JsonObject { ["."] = "grass", ["="] = "path", ["~"] = "water", ["^"] = "mountain" },
        ["rows"] = new JsonArray("^^^^", "^.=^", "^~.^"),
    };

    private static string With(Action<JsonObject> change)
    {
        var file = Valid();
        change(file);
        return file.ToJsonString();
    }

    private static MapFormatException AssertInvalid(string json, string expectedLocation)
    {
        var error = Assert.Throws<MapFormatException>(() => MapFile.Parse(json));
        Assert.Equal(expectedLocation, error.Location);
        Assert.Contains(expectedLocation, error.Message, StringComparison.Ordinal);
        return error;
    }

    [Fact]
    public void Valid_file_is_parsed()
    {
        var map = MapFile.Parse(Valid().ToJsonString());

        const GroundKind M = GroundKind.Mountain;
        const GroundKind G = GroundKind.Grass;
        const GroundKind P = GroundKind.Path;
        const GroundKind W = GroundKind.Water;
        GroundKind[] expected = [M, M, M, M, M, G, P, M, M, W, G, M];
        Assert.Equal(4, map.Width);
        Assert.Equal(3, map.Height);
        Assert.Equal(16, map.TileSize);
        Assert.Equal(-1, map.OriginTx);
        Assert.Equal(-2, map.OriginTy);
        Assert.Equal(expected, map.Ground);
        Assert.Equal(W, map.KindAt(0, 0));
    }

    [Fact]
    public void Several_characters_may_name_the_same_kind()
    {
        var map = MapFile.Parse(With(file =>
        {
            file["legend"]![","] = "grass";
            file["rows"]![1] = "^,=^";
        }));

        Assert.Equal(GroundKind.Grass, map.KindAt(0, -1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public void Unsupported_format_is_reported(int format) =>
        AssertInvalid(With(file => file["format"] = format), "$.format");

    [Theory]
    [InlineData("format")]
    [InlineData("name")]
    [InlineData("tileSize")]
    [InlineData("origin")]
    [InlineData("legend")]
    [InlineData("rows")]
    public void Missing_field_is_reported(string field) =>
        AssertInvalid(With(file => file.Remove(field)), $"$.{field}");

    [Theory]
    [InlineData("x")]
    [InlineData("y")]
    public void Missing_origin_coordinate_is_reported(string coordinate) =>
        AssertInvalid(With(file => file["origin"]!.AsObject().Remove(coordinate)), $"$.origin.{coordinate}");

    [Fact]
    public void Unknown_field_is_reported() =>
        AssertInvalid(With(file => file["objects"] = new JsonArray()), "$.objects");

    [Fact]
    public void Unknown_origin_field_is_reported() =>
        AssertInvalid(With(file => file["origin"]!["z"] = 0), "$.origin.z");

    [Fact]
    public void Empty_rows_are_reported() =>
        AssertInvalid(With(file => file["rows"] = new JsonArray()), "$.rows");

    [Fact]
    public void Empty_first_row_is_reported() =>
        AssertInvalid(With(file => file["rows"] = new JsonArray("")), "$.rows[0]");

    [Theory]
    [InlineData(1, "^.=")]
    [InlineData(2, "^~.^^")]
    public void Row_of_different_length_is_reported(int index, string row) =>
        AssertInvalid(With(file => file["rows"]![index] = row), $"$.rows[{index}]");

    [Fact]
    public void Unknown_character_is_reported_with_row_and_column() =>
        AssertInvalid(With(file => file["rows"]![1] = "^.x^"), "$.rows[1][2]");

    [Theory]
    [InlineData("lava")]
    [InlineData("Grass")]
    [InlineData("")]
    public void Unknown_kind_in_legend_is_reported(string kind) =>
        AssertInvalid(With(file => file["legend"]!["~"] = kind), "$.legend['~']");

    [Fact]
    public void Legend_kind_that_is_not_a_string_is_reported() =>
        AssertInvalid(With(file => file["legend"]!["~"] = 3), "$.legend['~']");

    [Theory]
    [InlineData("ab")]
    [InlineData("")]
    public void Legend_key_must_be_one_character(string key) =>
        AssertInvalid(With(file => file["legend"]![key] = "grass"), $"$.legend['{key}']");

    [Theory]
    [InlineData(-1, 0, "$.origin.x")]
    [InlineData(4, 0, "$.origin.x")]
    [InlineData(0, -1, "$.origin.y")]
    [InlineData(0, 3, "$.origin.y")]
    public void Origin_outside_the_map_is_reported(int x, int y, string location) =>
        AssertInvalid(With(file =>
        {
            file["origin"]!["x"] = x;
            file["origin"]!["y"] = y;
        }), location);

    [Fact]
    public void Origin_that_is_not_an_integer_is_reported() =>
        AssertInvalid(With(file => file["origin"]!["x"] = 1.5), "$.origin.x");

    [Theory]
    [InlineData(0)]
    [InlineData(-16)]
    public void Non_positive_tile_size_is_reported(int tileSize) =>
        AssertInvalid(With(file => file["tileSize"] = tileSize), "$.tileSize");

    [Fact]
    public void Fractional_tile_size_is_reported() =>
        AssertInvalid(With(file => file["tileSize"] = 16.5), "$.tileSize");

    [Theory]
    [InlineData("format", "\"1\"")]
    [InlineData("format", "null")]
    [InlineData("name", "1")]
    [InlineData("tileSize", "\"16\"")]
    [InlineData("origin", "[]")]
    [InlineData("legend", "\"x\"")]
    [InlineData("rows", "{}")]
    public void Field_of_wrong_type_is_reported(string field, string valueJson) =>
        AssertInvalid(With(file => file[field] = JsonNode.Parse(valueJson)), $"$.{field}");

    [Fact]
    public void Row_that_is_not_a_string_is_reported() =>
        AssertInvalid(With(file => file["rows"]![1] = 5), "$.rows[1]");

    [Fact]
    public void Duplicate_field_is_reported()
    {
        const string json = """
            {
              "format": 1,
              "name": "test",
              "tileSize": 16,
              "origin": { "x": 1, "y": 2 },
              "legend": { ".": "grass", "=": "path", "~": "water", "^": "mountain" },
              "rows": ["^^^^", "^.=^", "^~.^"],
              "rows": ["^^^^", "^.=^", "^~.^"]
            }
            """;

        AssertInvalid(json, "$.rows");
    }

    [Fact]
    public void Duplicate_legend_character_is_reported()
    {
        const string json = """
            {
              "format": 1,
              "name": "test",
              "tileSize": 16,
              "origin": { "x": 1, "y": 2 },
              "legend": { ".": "grass", ".": "path", "=": "path", "~": "water", "^": "mountain" },
              "rows": ["^^^^", "^.=^", "^~.^"]
            }
            """;

        AssertInvalid(json, "$.legend['.']");
    }

    [Fact]
    public void Root_that_is_not_an_object_is_reported() =>
        AssertInvalid("[]", "$");

    [Fact]
    public void Lone_surrogate_in_legend_value_is_reported() =>
        AssertInvalid(WithLoneSurrogate(file => file["legend"]!["~"] = Marker), "$.legend['~']");

    [Fact]
    public void Lone_surrogate_in_row_is_reported() =>
        AssertInvalid(WithLoneSurrogate(file => file["rows"]![2] = Marker), "$.rows[2]");

    [Fact]
    public void Lone_surrogate_in_field_name_is_reported() =>
        AssertInvalid(WithLoneSurrogate(file => file[Marker] = 1), "$");

    private const string Marker = "LONESURROGATE";

    private static string WithLoneSurrogate(Action<JsonObject> change)
    {
        var json = With(change);
        Assert.Contains(Marker, json, StringComparison.Ordinal);
        return json.Replace(Marker, "\\ud800", StringComparison.Ordinal);
    }

    [Fact]
    public void Invalid_json_is_reported_with_its_line()
    {
        var error = Assert.Throws<MapFormatException>(() => MapFile.Parse("{\n  \"format\": 1,\n  \"name\": \n}"));

        Assert.Equal("$", error.Location);
        Assert.Equal(4, error.Line);
        Assert.IsAssignableFrom<JsonException>(error.InnerException);
    }
}
