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
}

/// <summary>Fester Server-Takt: Ein- und Austritte übernehmen, Eingaben anwenden, simulieren, Snapshots verschicken.</summary>
internal sealed partial class GameLoop(ILogger<GameLoop> logger) : BackgroundService
{
    private readonly Channel<(PlayerConnection Connection, bool Joined)> _membership = Channel.CreateUnbounded<(PlayerConnection, bool)>();
    private readonly Dictionary<string, PlayerConnection> _connections = [];
    private readonly World _world = new(GameSettings.Speed, GameSettings.TickRateHz, new Random());
    private uint _tick;

    public PlayerConnection Join(string accountId, string name, (double X, double Y)? savedPosition)
    {
        var connection = new PlayerConnection(accountId, name, savedPosition);
        _membership.Writer.TryWrite((connection, true));
        return connection;
    }

    public void Leave(PlayerConnection connection)
    {
        connection.Outbox.Writer.TryComplete();
        _membership.Writer.TryWrite((connection, false));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / GameSettings.TickRateHz));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            Tick();
        }
    }

    private void Tick()
    {
        ApplyMembershipChanges();

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
    }

    private void ApplyMembershipChanges()
    {
        while (_membership.Reader.TryRead(out var change))
        {
            var (connection, joined) = change;
            if (joined)
            {
                var player = AddOrTakeOver(connection);
                var welcome = new Welcome { PlayerId = player.Id, Name = player.Name, Speed = (float)_world.Speed, TickRateHz = (uint)_world.TickRateHz };
                connection.Outbox.Writer.TryWrite(new ServerMessage { Welcome = welcome }.ToByteArray());
                LogJoined(player.Id, player.Name, _connections.Count);
            }
            else if (_connections.TryGetValue(connection.PlayerId, out var current) && ReferenceEquals(current, connection))
            {
                _connections.Remove(connection.PlayerId);
                _world.RemovePlayer(connection.PlayerId);
                LogLeft(connection.PlayerId, _connections.Count);
            }
        }
    }

    private Player AddOrTakeOver(PlayerConnection connection)
    {
        if (_connections.Remove(connection.PlayerId, out var previous))
        {
            // Derselbe Account ist schon verbunden: Nur die neueste Verbindung steuert die Figur, die alte bekommt nichts mehr.
            previous.Outbox.Writer.TryComplete();
        }

        var player = _world.GetPlayer(connection.PlayerId);
        if (player is not null)
        {
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

    private void BroadcastSnapshot()
    {
        var players = _world.Players
            .Select(p => new PlayerState { Id = p.Id, Name = p.Name, X = (float)p.X, Y = (float)p.Y, Color = p.Color })
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
}
