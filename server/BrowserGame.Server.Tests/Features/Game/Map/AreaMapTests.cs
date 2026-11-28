using BrowserGame.Server.Features.Game;
using GroundKind = BrowserGame.Contracts.V1.GroundKind;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Karte im Speicher (018 AK-5): Bodenart je Weltkachel, jenseits der Karte die äußerste Kachel, Weltpixel
/// → Weltkachel.
/// </summary>
public sealed class AreaMapTests
{
    private const GroundKind M = GroundKind.Mountain;
    private const GroundKind W = GroundKind.Water;
    private const GroundKind P = GroundKind.Path;
    private const GroundKind G = GroundKind.Grass;

    // 3 x 2, Ursprung Weltkachel (5, -3).
    private static AreaMap Map() => new(16, 3, 2, 5, -3, [M, W, P, G, P, W]);

    [Theory]
    [InlineData(5, -3, M)]
    [InlineData(6, -3, W)]
    [InlineData(7, -3, P)]
    [InlineData(5, -2, G)]
    [InlineData(6, -2, P)]
    [InlineData(7, -2, W)]
    public void Kind_inside_the_map_follows_the_origin(int tx, int ty, GroundKind expected) =>
        Assert.Equal(expected, Map().KindAt(tx, ty));

    [Theory]
    [InlineData(4, -3, M)]
    [InlineData(-1000, -2, G)]
    [InlineData(8, -3, P)]
    [InlineData(1000, -2, W)]
    [InlineData(5, -4, M)]
    [InlineData(6, -1000, W)]
    [InlineData(7, -4, P)]
    [InlineData(5, -1, G)]
    [InlineData(6, 1000, P)]
    [InlineData(7, -1, W)]
    public void Kind_beyond_an_edge_is_the_outermost_tile(int tx, int ty, GroundKind expected) =>
        Assert.Equal(expected, Map().KindAt(tx, ty));

    [Theory]
    [InlineData(4, -4, M)]
    [InlineData(8, -4, P)]
    [InlineData(4, -1, G)]
    [InlineData(8, -1, W)]
    public void Kind_beyond_a_corner_is_the_corner_tile(int tx, int ty, GroundKind expected) =>
        Assert.Equal(expected, Map().KindAt(tx, ty));

    [Theory]
    [InlineData(int.MinValue, int.MinValue, M)]
    [InlineData(int.MaxValue, int.MinValue, P)]
    [InlineData(int.MinValue, int.MaxValue, G)]
    [InlineData(int.MaxValue, int.MaxValue, W)]
    public void Kind_at_extreme_tiles_does_not_overflow(int tx, int ty, GroundKind expected) =>
        Assert.Equal(expected, Map().KindAt(tx, ty));

    [Theory]
    [InlineData(16, 0, 0, 0, 0)]
    [InlineData(16, 15.999, 16, 0, 1)]
    [InlineData(16, -0.001, -16, -1, -1)]
    [InlineData(16, -16.001, 31.5, -2, 1)]
    [InlineData(8, 8, -8.5, 1, -2)]
    public void TileOf_floors_world_pixels(int tileSize, double x, double y, int tx, int ty)
    {
        var map = new AreaMap(tileSize, 1, 1, 0, 0, [G]);

        Assert.Equal((tx, ty), map.TileOf(x, y));
    }

    [Fact]
    public void Far_away_position_shows_the_edge()
    {
        var map = Map();

        var (neTx, neTy) = map.TileOf(1e12, -1e12);
        var (swTx, swTy) = map.TileOf(-1e12, 1e12);

        Assert.Equal(P, map.KindAt(neTx, neTy));
        Assert.Equal(G, map.KindAt(swTx, swTy));
    }

    [Theory]
    [InlineData(0, 1, 1)]
    [InlineData(16, 0, 1)]
    [InlineData(16, 1, 0)]
    public void Non_positive_size_is_rejected(int tileSize, int width, int height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new AreaMap(tileSize, width, height, 0, 0, new GroundKind[width * height]));

    [Fact]
    public void Ground_of_wrong_length_is_rejected()
    {
        var error = Assert.Throws<ArgumentException>(() => new AreaMap(16, 3, 2, 0, 0, [M, W, P, G, P]));

        Assert.Equal("ground", error.ParamName);
    }
}
