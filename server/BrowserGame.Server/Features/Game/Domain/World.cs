namespace BrowserGame.Server.Features.Game;

/// <summary>Gewünschte Laufrichtung eines Spielers für genau einen Tick.</summary>
internal readonly record struct MoveInput(uint Seq, float DirX, float DirY);

internal sealed class Player(string id, string name, uint color, double x, double y)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public uint Color { get; } = color;
    public double X { get; set; } = x;
    public double Y { get; set; } = y;
    public uint LastProcessedSeq { get; set; }

    /// <summary>Verbindung weg, Figur wartet noch (003 AK-20).</summary>
    public bool Away => AwaySince.HasValue;

    /// <summary>Tick, ab dem die Figur wartet.</summary>
    internal uint? AwaySince { get; set; }

    internal uint LastQueuedSeq { get; set; }
    internal double DirX { get; set; }
    internal double DirY { get; set; }
    internal int TicksWithoutInput { get; set; }
    internal Queue<MoveInput> PendingInputs { get; } = new();
}

/// <summary>
/// Spielwelt ohne Netzwerk und Datenbank. Wird ausschließlich vom Tick-Thread verändert.
/// Der Server ist Autorität: Clients liefern nur Richtungen, die Strecke pro Tick bestimmt die Welt.
/// </summary>
internal sealed class World(double speed, int tickRateHz, Random random)
{
    /// <summary>Mehr gepufferte Eingaben bringen keinen Vorteil, ältere werden verworfen.</summary>
    public const int MaxQueuedInputs = 10;

    /// <summary>
    /// So viele Ticks läuft ein Spieler ohne neue Eingabe mit der letzten Richtung weiter
    /// (überbrückt Netzwerk-Jitter), danach bleibt er stehen (z. B. Tab im Hintergrund).
    /// </summary>
    public const int MaxCoastTicks = 2;

    public const double SpawnRadius = 64;

    private readonly Dictionary<string, Player> _players = [];

    public double Speed { get; } = speed;
    public int TickRateHz { get; } = tickRateHz;
    public double StepDistance => Speed / TickRateHz;
    public IReadOnlyCollection<Player> Players => _players.Values;

    /// <summary>Neue Figur am Startpunkt der Welt: zufällig im SpawnRadius um (0,0) (003 AK-17).</summary>
    public Player AddPlayer(string id, string name, uint color)
    {
        var angle = random.NextDouble() * 2 * Math.PI;
        var radius = random.NextDouble() * SpawnRadius;
        return AddPlayer(id, name, color, Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }

    public Player AddPlayer(string id, string name, uint color, double x, double y)
    {
        var player = new Player(id, name, color, x, y);
        _players.Add(id, player);
        return player;
    }

    /// <summary>Eine neue Verbindung steuert die Figur: Ihre Eingaben zählen wieder ab 1, alte Eingaben verfallen.</summary>
    public void RestartInputs(string id)
    {
        if (!_players.TryGetValue(id, out var player))
        {
            return;
        }

        player.PendingInputs.Clear();
        player.LastQueuedSeq = 0;
        player.LastProcessedSeq = 0;
        player.TicksWithoutInput = 0;
        (player.DirX, player.DirY) = (0, 0);
    }

    /// <summary>Verbindung weg: Figur bleibt stehen und nimmt keine Eingaben mehr an (003 AK-20).</summary>
    public void SetAway(string id, uint tick)
    {
        if (!_players.TryGetValue(id, out var player))
        {
            return;
        }

        player.AwaySince = tick;
        player.PendingInputs.Clear();
        player.TicksWithoutInput = 0;
        (player.DirX, player.DirY) = (0, 0);
    }

    /// <summary>Eine Verbindung übernimmt die wartende Figur wieder (003 AK-21).</summary>
    public void ClearAway(string id)
    {
        if (_players.TryGetValue(id, out var player))
        {
            player.AwaySince = null;
        }
    }

    /// <summary>Entfernt Figuren, die seit mindestens <paramref name="lingerTicks"/> Ticks warten, und liefert sie zurück.</summary>
    public IReadOnlyList<Player> RemoveExpiredAway(uint tick, uint lingerTicks)
    {
        var expired = _players.Values.Where(p => p.AwaySince is { } since && tick - since >= lingerTicks).ToList();
        foreach (var player in expired)
        {
            _players.Remove(player.Id);
        }

        return expired;
    }

    public bool RemovePlayer(string id) => _players.Remove(id);

    public Player? GetPlayer(string id) => _players.GetValueOrDefault(id);

    public void EnqueueInput(string playerId, MoveInput input)
    {
        if (!_players.TryGetValue(playerId, out var player) || player.Away || input.Seq <= player.LastQueuedSeq)
        {
            return;
        }

        player.LastQueuedSeq = input.Seq;
        player.PendingInputs.Enqueue(input);
        while (player.PendingInputs.Count > MaxQueuedInputs)
        {
            player.PendingInputs.Dequeue();
        }
    }

    public void Step()
    {
        foreach (var player in _players.Values)
        {
            if (player.Away)
            {
                continue;
            }

            if (player.PendingInputs.TryDequeue(out var input))
            {
                (player.DirX, player.DirY) = ClampDirection(input.DirX, input.DirY);
                player.LastProcessedSeq = input.Seq;
                player.TicksWithoutInput = 0;
            }
            else if (++player.TicksWithoutInput > MaxCoastTicks)
            {
                (player.DirX, player.DirY) = (0, 0);
            }

            player.X += player.DirX * StepDistance;
            player.Y += player.DirY * StepDistance;
        }
    }

    /// <summary>Kürzt auf Länge ≤ 1; ungültige Werte (NaN, ∞) bedeuten Stillstand.</summary>
    internal static (double X, double Y) ClampDirection(float x, float y)
    {
        if (!float.IsFinite(x) || !float.IsFinite(y))
        {
            return (0, 0);
        }

        var length = Math.Sqrt((double)x * x + (double)y * y);
        return length > 1 ? (x / length, y / length) : (x, y);
    }
}
