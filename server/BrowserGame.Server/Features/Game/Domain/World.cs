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

    /// <summary>Aktuelle HP, zwischen 0 und <see cref="World.MaxHp"/> (012 AK-1).</summary>
    public double Hp { get; internal set; }

    /// <summary>Aktuelles Mana, zwischen 0 und <see cref="World.MaxMana"/> (012 AK-1).</summary>
    public double Mana { get; internal set; }

    /// <summary>Zählt Sprünge der Figur (Respawn); ändert er sich, interpoliert der Client nicht (012 AK-8).</summary>
    public uint SpawnSeq { get; internal set; }

    /// <summary>
    /// Blickrichtung in Bogenmaß, 0 = +x, π/2 = +y (unten), Bereich (−π, π]. Folgt der Laufrichtung, bleibt im Stehen (016 AK-1–3).
    /// </summary>
    public double Facing { get; internal set; }

    /// <summary>Frühester Tick für den nächsten Angriff; nur ein erfolgreicher Angriff setzt ihn (013 AK-11, AK-12).</summary>
    public uint NextAttackTick { get; internal set; }

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
/// HP und Mana leben nur hier (012); Mana wächst in jedem Tick, HP nicht.
/// Die Blickrichtung folgt der angewandten Laufrichtung (016).
/// </summary>
internal sealed class World(
    double speed,
    int tickRateHz,
    Random random,
    double maxHp = World.DefaultMaxHp,
    double maxMana = World.DefaultMaxMana,
    double manaRegenPerSecond = World.DefaultManaRegenPerSecond,
    double startFacing = World.DefaultFacing,
    double minFacingInput = World.DefaultMinFacingInput)
{
    /// <summary>Mehr gepufferte Eingaben bringen keinen Vorteil, ältere werden verworfen.</summary>
    public const int MaxQueuedInputs = 10;

    /// <summary>
    /// So viele Ticks läuft ein Spieler ohne neue Eingabe mit der letzten Richtung weiter
    /// (überbrückt Netzwerk-Jitter), danach bleibt er stehen (z. B. Tab im Hintergrund).
    /// </summary>
    public const int MaxCoastTicks = 2;

    public const double SpawnRadius = 64;

    public const double DefaultMaxHp = 100;
    public const double DefaultMaxMana = 100;

    /// <summary>Leer bis voll in 20 s (012 AK-3).</summary>
    public const double DefaultManaRegenPerSecond = 5;

    /// <summary>Blickrichtung neuer Figuren: nach unten, zur Kamera (016 AK-4).</summary>
    public const double DefaultFacing = Math.PI / 2;

    /// <summary>Kürzere Richtungsvektoren ändern die Blickrichtung nicht (016 AK-5).</summary>
    public const double DefaultMinFacingInput = 0.01;

    private readonly Dictionary<string, Player> _players = [];

    public double Speed { get; } = speed;
    public int TickRateHz { get; } = tickRateHz;
    public double StepDistance => Speed / TickRateHz;
    public double MaxHp { get; } = maxHp;
    public double MaxMana { get; } = maxMana;
    public double ManaRegenPerSecond { get; } = manaRegenPerSecond;
    public double ManaRegenPerTick => ManaRegenPerSecond / TickRateHz;
    public double StartFacing { get; } = startFacing;
    public double MinFacingInput { get; } = minFacingInput;
    public IReadOnlyCollection<Player> Players => _players.Values;

    /// <summary>Neue Figur am Startpunkt der Welt: zufällig im SpawnRadius um (0,0) (003 AK-17).</summary>
    public Player AddPlayer(string id, string name, uint color)
    {
        var (x, y) = RandomStartPosition();
        return AddPlayer(id, name, color, x, y);
    }

    public Player AddPlayer(string id, string name, uint color, double x, double y)
    {
        var player = new Player(id, name, color, x, y) { Hp = MaxHp, Mana = MaxMana, Facing = StartFacing };
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

    /// <summary>
    /// Senkt die HP um <paramref name="amount"/> (012 AK-7). Bei HP ≤ 0 stirbt die Figur und erscheint im selben Aufruf
    /// neu (AK-8, AK-9). Liefert, ob die Figur gestorben ist.
    /// </summary>
    public bool Damage(string playerId, double amount)
    {
        if (!(amount > 0) || !_players.TryGetValue(playerId, out var player))
        {
            return false;
        }

        player.Hp -= amount;
        if (player.Hp > 0)
        {
            return false;
        }

        Respawn(player);
        return true;
    }

    /// <summary>
    /// Setzt HP und/oder Mana direkt, begrenzt auf [0, Max] (012 AK-1, AK-10). <c>null</c> und NaN lassen den Wert unverändert.
    /// HP ≤ 0 heißt tot: sofortiger Respawn (AK-8, AK-9).
    /// </summary>
    public void SetVitals(string playerId, double? hp = null, double? mana = null)
    {
        if (!_players.TryGetValue(playerId, out var player))
        {
            return;
        }

        if (mana is { } newMana && !double.IsNaN(newMana))
        {
            player.Mana = Math.Clamp(newMana, 0, MaxMana);
        }

        if (hp is { } newHp && !double.IsNaN(newHp))
        {
            player.Hp = Math.Clamp(newHp, 0, MaxHp);
            if (player.Hp <= 0)
            {
                Respawn(player);
            }
        }
    }

    /// <summary>Tote Figur erscheint am Startpunkt der Welt mit vollen Werten (012 AK-8).</summary>
    private void Respawn(Player player)
    {
        (player.X, player.Y) = RandomStartPosition();
        player.SpawnSeq++;
        player.PendingInputs.Clear();
        // Verworfene Eingaben gelten als verarbeitet, sonst wendet der Client sie auf die neue Position an.
        player.LastProcessedSeq = player.LastQueuedSeq;
        player.TicksWithoutInput = 0;
        (player.DirX, player.DirY) = (0, 0);

        player.Hp = MaxHp;
        player.Mana = MaxMana;
    }

    /// <summary>Zufälliger Punkt im SpawnRadius um (0,0), der Startpunkt der Welt (003 AK-17).</summary>
    private (double X, double Y) RandomStartPosition()
    {
        var angle = random.NextDouble() * 2 * Math.PI;
        var radius = random.NextDouble() * SpawnRadius;
        return (Math.Cos(angle) * radius, Math.Sin(angle) * radius);
    }

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
            player.Mana = Math.Min(MaxMana, player.Mana + ManaRegenPerTick);

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

            UpdateFacing(player);

            player.X += player.DirX * StepDistance;
            player.Y += player.DirY * StepDistance;
        }
    }

    /// <summary>
    /// Blickrichtung aus der angewandten Laufrichtung; zu kurze Vektoren und Stillstand lassen sie stehen (016 AK-1–3, AK-5).
    /// Stillstand zählt nie, auch nicht bei <see cref="MinFacingInput"/> = 0.
    /// </summary>
    private void UpdateFacing(Player player)
    {
        var length = Math.Sqrt(player.DirX * player.DirX + player.DirY * player.DirY);
        if (length > 0 && length >= MinFacingInput)
        {
            player.Facing = Math.Atan2(player.DirY, player.DirX);
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
