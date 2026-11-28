using BrowserGame.Contracts.V1;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Feste Karte eines Gebiets (018): Bodenart je Kachel. Jenseits der Karte setzt sich die äußerste Kachel endlos fort
/// (AK-5). Koordinaten wie im Vertrag: Weltkachel (tx, ty) = Kartenkachel (tx − OriginTx, ty − OriginTy).
/// </summary>
internal sealed class AreaMap
{
    public AreaMap(int tileSize, int width, int height, int originTx, int originTy, GroundKind[] ground)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(tileSize);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentNullException.ThrowIfNull(ground);
        if (ground.Length != (long)width * height)
        {
            throw new ArgumentException("Länge von ground muss width × height sein.", nameof(ground));
        }

        TileSize = tileSize;
        Width = width;
        Height = height;
        OriginTx = originTx;
        OriginTy = originTy;
        Ground = ground;
    }

    /// <summary>Kantenlänge einer Kachel in Weltpixeln.</summary>
    public int TileSize { get; }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Weltkachel der Kartenkachel (0, 0).</summary>
    public int OriginTx { get; }

    /// <summary>Weltkachel der Kartenkachel (0, 0).</summary>
    public int OriginTy { get; }

    /// <summary>Eine Bodenart je Kachel, Zeile für Zeile von oben links; Länge Width × Height.</summary>
    public GroundKind[] Ground { get; }

    /// <summary>Bodenart der Weltkachel; außerhalb der Karte die nächste Randkachel (Plan-Entscheidung 3).</summary>
    public GroundKind KindAt(int tx, int ty)
    {
        var mx = (int)Math.Clamp((long)tx - OriginTx, 0, Width - 1);
        var my = (int)Math.Clamp((long)ty - OriginTy, 0, Height - 1);
        return Ground[(my * Width) + mx];
    }

    /// <summary>Weltkachel eines Weltpunkts.</summary>
    public (int Tx, int Ty) TileOf(double x, double y) =>
        ((int)Math.Floor(x / TileSize), (int)Math.Floor(y / TileSize));
}
