using BrowserGame.Server.Features.Game;
using GroundKind = BrowserGame.Contracts.V1.GroundKind;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Die echte Karte des ersten Gebiets (018 AK-4), so wie sie neben der Anwendung liegt: Größe, Startpunkt, Bergrand, Weg,
/// See und die Regeln aus Plan-Entscheidung 5, damit die 13 Übergangsformen reichen.
/// </summary>
public sealed class MeadowMapTests
{
    private const GroundKind M = GroundKind.Mountain;
    private const GroundKind W = GroundKind.Water;
    private const GroundKind P = GroundKind.Path;
    private const GroundKind G = GroundKind.Grass;

    // Wie MOUNTAIN_BAND in scripts/generate-map.py (plan.md, Konstanten).
    private const int MountainBand = 4;

    private static readonly string MeadowPath = Path.Combine(AppContext.BaseDirectory, "Maps", "meadow.json");

    private static AreaMap Load() => MapFile.Parse(File.ReadAllText(MeadowPath));

    private static GroundKind At(AreaMap map, int x, int y) => map.Ground[(y * map.Width) + x];

    [Fact]
    public void Meadow_is_copied_next_to_the_application() =>
        Assert.True(File.Exists(MeadowPath), $"{MeadowPath} fehlt in der Ausgabe");

    [Fact]
    public void Meadow_is_ten_by_eight_screens()
    {
        var map = Load();

        Assert.Equal(16, map.TileSize);
        Assert.Equal(400, map.Width);
        Assert.Equal(180, map.Height);
        Assert.Equal(10 * 640, map.Width * map.TileSize);
        Assert.Equal(8 * 360, map.Height * map.TileSize);
    }

    [Fact]
    public void Start_is_in_the_middle_of_the_map()
    {
        var map = Load();

        Assert.Equal(-200, map.OriginTx);
        Assert.Equal(-90, map.OriginTy);
    }

    [Fact]
    public void Start_tile_is_path_or_grass() =>
        Assert.Contains(Load().KindAt(0, 0), new[] { P, G });

    [Fact]
    public void Mountains_form_the_border_and_nothing_else()
    {
        var map = Load();
        var wrong = new List<(int X, int Y)>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var inBand = x < MountainBand || y < MountainBand
                    || x >= map.Width - MountainBand || y >= map.Height - MountainBand;
                if ((At(map, x, y) == M) != inBand)
                {
                    wrong.Add((x, y));
                }
            }
        }

        Assert.Empty(wrong);
    }

    [Fact]
    public void Meadow_is_mostly_grass_with_path_and_water()
    {
        var map = Load();

        Assert.True(map.Ground.Count(kind => kind == G) > map.Ground.Length / 2, "Gras ist nicht die Mehrheit");
        Assert.Contains(P, map.Ground);
        Assert.Contains(W, map.Ground);
    }

    [Fact]
    public void Water_is_one_lake()
    {
        var map = Load();
        var water = map.Ground.Count(kind => kind == W);
        var first = Array.IndexOf(map.Ground, W);
        Assert.True(first >= 0, "kein Wasser");

        Assert.Equal(water, ConnectedCount(map, first % map.Width, first / map.Width));
    }

    [Fact]
    public void Path_and_water_lie_in_two_by_two_blocks() => Assert.Empty(OutsideTwoByTwo(Load()));

    [Fact]
    public void Different_non_grass_kinds_are_separated_by_grass() => Assert.Empty(TouchingKinds(Load()));

    [Fact]
    public void Rule_checks_find_violations()
    {
        // Weg eine Kachel breit; Weg diagonal neben Wasser; Weg und Wasser als 2×2-Blöcke mit Gras dazwischen.
        var narrow = new AreaMap(16, 3, 1, 0, 0, [G, P, G]);
        var touching = new AreaMap(16, 4, 4, 0, 0,
        [
            P, P, G, G,
            P, P, G, G,
            G, G, W, W,
            G, G, W, W,
        ]);
        var mirrored = new AreaMap(16, 4, 4, 0, 0,
        [
            G, G, P, P,
            G, G, P, P,
            W, W, G, G,
            W, W, G, G,
        ]);
        var fine = new AreaMap(16, 5, 2, 0, 0, [P, P, G, W, W, P, P, G, W, W]);

        Assert.Contains((1, 0), OutsideTwoByTwo(narrow));
        Assert.Contains(((1, 1), (2, 2)), TouchingKinds(touching));
        Assert.Contains(((2, 1), (1, 2)), TouchingKinds(mirrored));
        Assert.Empty(OutsideTwoByTwo(fine));
        Assert.Empty(TouchingKinds(fine));
    }

    // Weg- und Wasserkacheln, die in keinem 2×2-Block derselben Art liegen (Plan-Entscheidung 5).
    private static List<(int X, int Y)> OutsideTwoByTwo(AreaMap map)
    {
        var outside = new List<(int X, int Y)>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var kind = At(map, x, y);
                if ((kind == P || kind == W) && !InTwoByTwo(map, x, y, kind))
                {
                    outside.Add((x, y));
                }
            }
        }

        return outside;
    }

    private static bool InTwoByTwo(AreaMap map, int x, int y, GroundKind kind)
    {
        for (var left = x - 1; left <= x; left++)
        {
            for (var top = y - 1; top <= y; top++)
            {
                if (left >= 0 && top >= 0 && left + 1 < map.Width && top + 1 < map.Height
                    && At(map, left, top) == kind && At(map, left + 1, top) == kind
                    && At(map, left, top + 1) == kind && At(map, left + 1, top + 1) == kind)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // Nachbarpaare (auch diagonal) aus zwei verschiedenen Nicht-Gras-Arten (Plan-Entscheidung 5).
    private static List<((int X, int Y) A, (int X, int Y) B)> TouchingKinds(AreaMap map)
    {
        var touching = new List<((int X, int Y) A, (int X, int Y) B)>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var kind = At(map, x, y);
                if (kind == G)
                {
                    continue;
                }

                // Rechts, unten links, unten, unten rechts: jedes Paar genau einmal.
                foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y + 1), (x, y + 1), (x + 1, y + 1) })
                {
                    if (nx < 0 || nx >= map.Width || ny >= map.Height)
                    {
                        continue;
                    }

                    var other = At(map, nx, ny);
                    if (other != G && other != kind)
                    {
                        touching.Add(((x, y), (nx, ny)));
                    }
                }
            }
        }

        return touching;
    }

    // Größe der 4-zusammenhängenden Fläche derselben Art ab (x, y).
    private static int ConnectedCount(AreaMap map, int x, int y)
    {
        var kind = At(map, x, y);
        var seen = new bool[map.Ground.Length];
        var open = new Stack<(int X, int Y)>();
        open.Push((x, y));
        seen[(y * map.Width) + x] = true;
        var count = 0;
        while (open.Count > 0)
        {
            var (cx, cy) = open.Pop();
            count++;
            foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
            {
                if (nx >= 0 && nx < map.Width && ny >= 0 && ny < map.Height
                    && !seen[(ny * map.Width) + nx] && At(map, nx, ny) == kind)
                {
                    seen[(ny * map.Width) + nx] = true;
                    open.Push((nx, ny));
                }
            }
        }

        return count;
    }
}
