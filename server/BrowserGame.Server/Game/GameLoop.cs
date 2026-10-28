using System.Threading.Channels;
using BrowserGame.Contracts.V1;
using Google.Protobuf;

namespace BrowserGame.Server.Game;

/// <summary>Verbindung eines Spielers zwischen WebSocket-Handler und Tick-Thread.</summary>
internal sealed class PlayerConnection(string playerId, string name, (double X, double Y)? savedPosition)
{
    public string PlayerId { get; } = playerId;
    public string Name { get; } = name;

    /// <summary>Zuletzt gespeicherte Position; ohne sie startet die Figur am Startpunkt (AK-17, AK-18).</summary>
    public (double X, double Y)? SavedPosition { get; } = savedPosition;

    /// <summary>Eingaben vom Client; bei Flut fallen die ältesten weg.</summary>
    public Channel<MoveInput> Inbox { get; } = Channel.CreateBounded<MoveInput>(
        new BoundedChannelOptions(World.MaxQueuedInputs * 2) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });

    /// <summary>Serialisierte Server-Nachrichten; ein langsamer Client verpasst alte Snapshots statt den Tick zu bremsen.</summary>
    public Channel<byte[]> Outbox { get; } = Channel.CreateBounded<byte[]>(
        new BoundedChannelOptions(8) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });

    /// <summary>Vom Server beendet (Kicked steht als letzte Nachricht in der Outbox); der Endpoint schließt dann den Socket.</summary>
    public bool WasKicked { get; set; }
}

/// <summary>
/// Fester Server-Takt: Ein- und Austritte übernehmen, Eingaben anwenden, simulieren, Snapshots verschicken.
/// Dazu Nachlauf getrennter Figuren, Übernahme und Rauswurf (003 AK-12, AK-20–22).
/// </summary>
public sealed partial class GameLoop(ILogger<GameLoop> logger, IConfiguration configuration) : BackgroundService
{
    private enum ChangeKind { Join, Leave, ForceLeave, ForceLeaveWithoutSave }

    private readonly record struct Change(ChangeKind Kind, string PlayerId, PlayerConnection? Connection);

    private readonly Channel<Change> _changes = Channel.CreateUnbounded<Change>();
    private readonly Dictionary<string, PlayerConnection> _connections = [];
    private readonly World _world = new(GameSettings.Speed, GameSettings.TickRateHz, new Random());
    private readonly uint _lingerTicks = GameSettings.ToTicks(configuration.GetValue(GameSettings.LingerSecondsKey, GameSettings.LingerSeconds));
    private uint _tick;

    internal PlayerConnection Join(string accountId, string name, (double X, double Y)? savedPosition)
    {
        var connection = new PlayerConnection(accountId, name, savedPosition);
        _changes.Writer.TryWrite(new Change(ChangeKind.Join, accountId, connection));
        return connection;
    }

    internal void Leave(PlayerConnection connection)
    {
        connection.Outbox.Writer.TryComplete();
        _changes.Writer.TryWrite(new Change(ChangeKind.Leave, connection.PlayerId, connection));
    }

    /// <summary>
    /// Abmelden, Passwort zurückgesetzt, Account gelöscht: Figur sofort entfernen, Verbindung mit Kicked(SIGNED_OUT) beenden
    /// (003 AK-15, AK-16, AK-22). Ohne <paramref name="savePosition"/> wird die Position nicht gespeichert (Löschen).
    /// </summary>
    public void ForceLeave(string accountId, bool savePosition) =>
        _changes.Writer.TryWrite(new Change(savePosition ? ChangeKind.ForceLeave : ChangeKind.ForceLeaveWithoutSave, accountId, null));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / GameSettings.TickRateHz));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            // 003 T12: Tick() liefert die Figuren, deren Position zu speichern ist (AK-18, AK-23) – an den PositionStore geben.
            _ = Tick();
        }
    }

    /// <summary>Ein Server-Tick. Liefert die Figuren, die die Welt verlassen haben und deren Position zu speichern ist.</summary>
    internal IReadOnlyList<Player> Tick()
    {
        var removed = new List<Player>();
        ApplyChanges(removed);

        foreach (var player in _world.RemoveExpiredAway(_tick, _lingerTicks))
        {
            removed.Add(player);
            LogLeft(player.Id, _connections.Count);
        }

        foreach (var connection in _connections.Values)
        {
            while (connection.Inbox.Reader.TryRead(out var input))
            {
                _world.EnqueueInput(connection.PlayerId, input);
            }
        }

        _world.Step();
        _tick++;
        BroadcastSnapshot();
        return removed;
    }

    private void ApplyChanges(List<Player> removed)
    {
        while (_changes.Reader.TryRead(out var change))
        {
            switch (change.Kind)
            {
                case ChangeKind.Join:
                    var joined = AddOrTakeOver(change.Connection!);
                    var welcome = new Welcome { PlayerId = joined.Id, Name = joined.Name, Speed = (float)_world.Speed, TickRateHz = (uint)_world.TickRateHz };
                    change.Connection!.Outbox.Writer.TryWrite(new ServerMessage { Welcome = welcome }.ToByteArray());
                    LogJoined(joined.Id, joined.Name, _connections.Count);
                    break;

                case ChangeKind.Leave:
                    if (_connections.TryGetValue(change.PlayerId, out var current) && ReferenceEquals(current, change.Connection))
                    {
                        _connections.Remove(change.PlayerId);
                        _world.SetAway(change.PlayerId, _tick);
                        LogAway(change.PlayerId, _connections.Count);
                    }

                    break;

                case ChangeKind.ForceLeave:
                case ChangeKind.ForceLeaveWithoutSave:
                    if (_connections.Remove(change.PlayerId, out var connection))
                    {
                        Kick(connection, Kicked.Types.Reason.SignedOut);
                    }

                    if (_world.GetPlayer(change.PlayerId) is { } player)
                    {
                        _world.RemovePlayer(player.Id);
                        if (change.Kind == ChangeKind.ForceLeave)
                        {
                            removed.Add(player);
                        }

                        LogLeft(player.Id, _connections.Count);
                    }

                    break;
            }
        }
    }

    private Player AddOrTakeOver(PlayerConnection connection)
    {
        if (_connections.Remove(connection.PlayerId, out var previous))
        {
            // Anmeldung auf einem anderen Gerät: Die alte Verbindung fliegt raus, die Figur bleibt (003 AK-12).
            Kick(previous, Kicked.Types.Reason.SignedInElsewhere);
            LogTakenOver(connection.PlayerId);
        }

        var player = _world.GetPlayer(connection.PlayerId);
        if (player is not null)
        {
            _world.ClearAway(player.Id);
            _world.RestartInputs(player.Id);
        }
        else
        {
            var color = PlayerColors.For(connection.PlayerId);
            player = connection.SavedPosition is { } saved
                ? _world.AddPlayer(connection.PlayerId, connection.Name, color, saved.X, saved.Y)
                : _world.AddPlayer(connection.PlayerId, connection.Name, color);
        }

        _connections.Add(player.Id, connection);
        return player;
    }

    private static void Kick(PlayerConnection connection, Kicked.Types.Reason reason)
    {
        connection.WasKicked = true;
        connection.Outbox.Writer.TryWrite(new ServerMessage { Kicked = new Kicked { Reason = reason } }.ToByteArray());
        connection.Outbox.Writer.TryComplete();
    }

    private void BroadcastSnapshot()
    {
        var players = _world.Players
            .Select(p => new PlayerState { Id = p.Id, Name = p.Name, X = (float)p.X, Y = (float)p.Y, Color = p.Color, Away = p.Away })
            .ToList();

        foreach (var connection in _connections.Values)
        {
            var snapshot = new Snapshot { Tick = _tick, LastProcessedSeq = _world.GetPlayer(connection.PlayerId)?.LastProcessedSeq ?? 0 };
            snapshot.Players.AddRange(players);
            connection.Outbox.Writer.TryWrite(new ServerMessage { Snapshot = snapshot }.ToByteArray());
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} ({Name}) beigetreten, {Count} online")]
    private partial void LogJoined(string playerId, string name, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} gegangen, {Count} online")]
    private partial void LogLeft(string playerId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} getrennt, Figur wartet, {Count} online")]
    private partial void LogAway(string playerId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} auf anderem Gerät angemeldet, alte Verbindung beendet")]
    private partial void LogTakenOver(string playerId);
}
