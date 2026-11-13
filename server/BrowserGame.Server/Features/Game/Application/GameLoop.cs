using System.Threading.Channels;
using BrowserGame.Contracts.V1;
using Google.Protobuf;
using ProtoChatHistory = BrowserGame.Contracts.V1.ChatHistory;

namespace BrowserGame.Server.Features.Game;

/// <summary>Verbindung eines Spielers zwischen WebSocket-Handler und Tick-Thread.</summary>
internal sealed class PlayerConnection(
    string playerId, string name, (double X, double Y)? savedPosition, System.Action? onOverflow = null, KeyBindings? keyBindings = null)
{
    public string PlayerId { get; } = playerId;
    public string Name { get; } = name;

    /// <summary>Zuletzt gespeicherte Position; ohne sie startet die Figur am Startpunkt (AK-17, AK-18).</summary>
    public (double X, double Y)? SavedPosition { get; } = savedPosition;

    /// <summary>Gespeicherte Tastenbelegung des Accounts für Welcome (002 AK-9); leer = Standard des Clients (AK-10).</summary>
    public KeyBindings KeyBindings { get; } = keyBindings ?? new KeyBindings();

    /// <summary>Eingaben vom Client; bei Flut fallen die ältesten weg.</summary>
    public Channel<MoveInput> Inbox { get; } = Channel.CreateBounded<MoveInput>(
        new BoundedChannelOptions(World.MaxQueuedInputs * 2) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });

    /// <summary>Chat-Texte vom Client, noch ungeprüft (011); bei Flut fallen die ältesten weg.</summary>
    public Channel<string> Chat { get; } = Channel.CreateBounded<string>(
        new BoundedChannelOptions(GameSettings.ChatInboxCapacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true });

    /// <summary>Serialisierte Server-Nachrichten; ein langsamer Client verpasst Snapshots statt den Tick zu bremsen.</summary>
    public PlayerOutbox Outbox { get; } = new(GameSettings.OutboxRequiredLimit, onOverflow);

    /// <summary>Vom Server beendet (Kicked steht als letzte Nachricht in der Outbox); der Endpoint schließt dann den Socket.</summary>
    public bool WasKicked { get; set; }
}

/// <summary>
/// Fester Server-Takt: Ein- und Austritte übernehmen, Eingaben anwenden, Chat verteilen, simulieren, Snapshots verschicken.
/// Dazu Nachlauf getrennter Figuren, Übernahme und Rauswurf (003 AK-12, AK-20–22); Positionen an den PositionStore (AK-18, AK-19).
/// </summary>
public sealed partial class GameLoop(ILogger<GameLoop> logger, IConfiguration configuration, TimeProvider? time = null) : BackgroundService
{
    private enum ChangeKind { Join, Leave, ForceLeave, ForceLeaveWithoutSave, ChatHistoryLoaded, RemoveChat, SetVitals }

    private readonly record struct Change(
        ChangeKind Kind, string PlayerId, PlayerConnection? Connection, IReadOnlyList<ChatHistoryEntry>? ChatHistory = null,
        double? Hp = null, double? Mana = null);

    private readonly Channel<Change> _changes = Channel.CreateUnbounded<Change>();
    private static readonly byte[] ChatTooFast =
        new ServerMessage { ChatRejected = new ChatRejected { Reason = ChatRejected.Types.Reason.TooFast } }.ToByteArray();

    private readonly TimeProvider _time = time ?? TimeProvider.System;
    private readonly ChatRateLimiter _chatLimiter = new(GameSettings.ChatRateLimitCount, TimeSpan.FromSeconds(GameSettings.ChatRateLimitSeconds));
    private readonly ChatHistory _chatHistory = new(GameSettings.ChatHistoryCount, TimeSpan.FromHours(GameSettings.ChatHistoryHours));
    // Gelöschte Accounts (011 AK-17). Nur der Tick-Thread; wächst nur mit Löschungen und endet mit dem Neustart.
    private readonly HashSet<string> _chatRemoved = [];
    private readonly Dictionary<string, PlayerConnection> _connections = [];
    private readonly World _world = new(
        GameSettings.Speed, GameSettings.TickRateHz, new Random(),
        GameSettings.MaxHp, GameSettings.MaxMana, GameSettings.ManaRegenPerSecond);
    private readonly uint _lingerTicks = GameSettings.ToTicks(configuration.GetValue(GameSettings.LingerSecondsKey, GameSettings.LingerSeconds));

    // Mindestens 1 Tick, sonst Division durch 0 im Speicher-Takt.
    private readonly uint _saveIntervalTicks = Math.Max(1u, GameSettings.ToTicks(
        configuration.GetValue(GameSettings.PositionSaveIntervalSecondsKey, GameSettings.PositionSaveIntervalSeconds)));

    private readonly Channel<IReadOnlyList<PositionToSave>> _positionBatches = Channel.CreateUnbounded<IReadOnlyList<PositionToSave>>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

    private uint _tick;

    /// <summary>Zu speichernde Positionen für den <see cref="PositionStore"/>; wird beim Herunterfahren geschlossen.</summary>
    internal ChannelReader<IReadOnlyList<PositionToSave>> PositionBatches => _positionBatches.Reader;

    /// <summary>Angenommene Chat-Nachrichten für den <see cref="ChatStore"/> (011 AK-15).</summary>
    internal ChatSaveQueue ChatToSave { get; } = new(GameSettings.ChatSaveQueueCapacity);

    /// <summary>
    /// Verlauf aus der Datenbank übernehmen, älteste zuerst (011 AK-15). Nur beim Start, bevor der GameLoop tickt: Er wird im
    /// Tick-Thread vor allen späteren Beitritten eingetragen.
    /// </summary>
    internal void LoadChatHistory(IReadOnlyList<ChatHistoryEntry> messages) =>
        _changes.Writer.TryWrite(new Change(ChangeKind.ChatHistoryLoaded, "", null, messages));

    internal PlayerConnection Join(
        string accountId, string name, (double X, double Y)? savedPosition, System.Action? abort = null, KeyBindings? keyBindings = null)
    {
        var connection = new PlayerConnection(accountId, name, savedPosition, () =>
        {
            LogStalled(accountId);
            abort?.Invoke();
        }, keyBindings);
        _changes.Writer.TryWrite(new Change(ChangeKind.Join, accountId, connection));
        return connection;
    }

    internal void Leave(PlayerConnection connection)
    {
        connection.Outbox.Close();
        _changes.Writer.TryWrite(new Change(ChangeKind.Leave, connection.PlayerId, connection));
    }

    /// <summary>
    /// Abmelden, Passwort zurückgesetzt, Account gelöscht: Figur sofort entfernen, Verbindung mit Kicked(SIGNED_OUT) beenden
    /// (003 AK-15, AK-16, AK-22). Ohne <paramref name="savePosition"/> wird die Position nicht gespeichert (Löschen).
    /// </summary>
    public void ForceLeave(string accountId, bool savePosition) =>
        _changes.Writer.TryWrite(new Change(savePosition ? ChangeKind.ForceLeave : ChangeKind.ForceLeaveWithoutSave, accountId, null));

    /// <summary>
    /// Account gelöscht: Seine Nachrichten verlassen den Verlauf, von ihm wird nichts mehr angenommen (011 AK-17). Nach
    /// <see cref="ForceLeave"/> aufrufen. Warten ist nicht nötig: Ein späterer Beitritt steht im selben Channel dahinter.
    /// </summary>
    public void RemoveChatOf(string accountId) =>
        _changes.Writer.TryWrite(new Change(ChangeKind.RemoveChat, accountId, null));

    /// <summary>
    /// Setzt HP und/oder Mana einer Figur im nächsten Tick, von jedem Thread aus (012 AK-10). <c>null</c> lässt den Wert
    /// unverändert; HP ≤ 0 heißt sofortiger Respawn (AK-8, AK-9). Unbekannte Accounts werden ignoriert.
    /// </summary>
    internal void SetVitals(string accountId, double? hp = null, double? mana = null) =>
        _changes.Writer.TryWrite(new Change(ChangeKind.SetVitals, accountId, null, Hp: hp, Mana: mana));

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1.0 / GameSettings.TickRateHz));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                TickAndQueuePositions();
            }
        }
        finally
        {
            // Herunterfahren: letzter Stand aller Figuren an den PositionStore (003 AK-19).
            FlushPositions();
        }
    }

    /// <summary>
    /// Ein Tick im laufenden Server: Entfernte Figuren gehen sofort an den PositionStore, alle Figuren (auch wartende)
    /// im Speicher-Takt (003 AK-18, AK-19, AK-23).
    /// </summary>
    internal void TickAndQueuePositions()
    {
        var removed = Tick();
        if (_tick % _saveIntervalTicks == 0)
        {
            QueuePositions([.. removed, .. _world.Players]);
        }
        else
        {
            QueuePositions(removed);
        }
    }

    /// <summary>Herunterfahren: alle Figuren speichern, danach nimmt der Channel nichts mehr an (003 AK-19).</summary>
    internal void FlushPositions()
    {
        QueuePositions([.. _world.Players]);
        _positionBatches.Writer.TryComplete();
    }

    private void QueuePositions(IReadOnlyList<Player> players)
    {
        if (players.Count > 0)
        {
            _positionBatches.Writer.TryWrite([.. players.Select(p => new PositionToSave(p.Id, p.X, p.Y))]);
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

        ProcessChat();

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
                    var welcome = new Welcome
                    {
                        PlayerId = joined.Id,
                        Name = joined.Name,
                        Speed = (float)_world.Speed,
                        TickRateHz = (uint)_world.TickRateHz,
                        MaxHp = (float)_world.MaxHp,
                        MaxMana = (float)_world.MaxMana,
                        KeyBindings = change.Connection!.KeyBindings,
                    };
                    change.Connection!.Outbox.SendRequired(new ServerMessage { Welcome = welcome }.ToByteArray());
                    SendChatHistory(change.Connection);
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

                case ChangeKind.ChatHistoryLoaded:
                    foreach (var entry in change.ChatHistory!)
                    {
                        _chatHistory.Add(entry);
                    }

                    break;

                case ChangeKind.RemoveChat:
                    _chatHistory.RemoveAccount(change.PlayerId);
                    _chatRemoved.Add(change.PlayerId);
                    break;

                case ChangeKind.SetVitals:
                    _world.SetVitals(change.PlayerId, change.Hp, change.Mana);
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
        connection.Outbox.Close(new ServerMessage { Kicked = new Kicked { Reason = reason } }.ToByteArray());
    }

    /// <summary>Direkt nach Welcome, in derselben Warteschlange (011 AK-15). Ohne Verlauf geht nichts raus; der Client leert seine Liste bei Welcome.</summary>
    private void SendChatHistory(PlayerConnection connection)
    {
        var recent = _chatHistory.Recent(_time.GetUtcNow());
        if (recent.Count == 0)
        {
            return;
        }

        var history = new ProtoChatHistory();
        history.Messages.AddRange(recent.Select(entry => new ChatMessage
        {
            SenderName = entry.SenderName,
            Text = entry.Text,
            SentAtUnixMs = entry.SentAt.ToUnixTimeMilliseconds(),
        }));
        connection.Outbox.SendRequired(new ServerMessage { ChatHistory = history }.ToByteArray());
    }

    private void ProcessChat()
    {
        var now = _time.GetUtcNow();
        foreach (var connection in _connections.Values)
        {
            while (connection.Chat.Reader.TryRead(out var text))
            {
                HandleChat(connection, text, now);
            }
        }
    }

    private void HandleChat(PlayerConnection sender, string rawText, DateTimeOffset now)
    {
        // Verbindung, die dem Löschen zuvorkam und erst danach beigetreten ist.
        if (_chatRemoved.Contains(sender.PlayerId))
        {
            return;
        }

        if (ChatRules.Normalize(rawText, GameSettings.ChatMaxLength) is not { } text)
        {
            return;
        }

        if (!_chatLimiter.TryAccept(sender.PlayerId, now))
        {
            sender.Outbox.SendRequired(ChatTooFast);
            return;
        }

        var senderName = _world.GetPlayer(sender.PlayerId)?.Name ?? sender.Name;
        var entry = new ChatHistoryEntry(sender.PlayerId, senderName, text, now);
        _chatHistory.Add(entry);
        ChatToSave.Add(entry);

        var message = new ChatMessage
        {
            SenderName = senderName,
            Text = text,
            SentAtUnixMs = now.ToUnixTimeMilliseconds(),
        };

        // Einmal serialisiert, alle Verbindungen teilen das Array; es wird nur gelesen.
        var bytes = new ServerMessage { ChatMessage = message }.ToByteArray();
        foreach (var connection in _connections.Values)
        {
            connection.Outbox.SendRequired(bytes);
        }
    }

    private void BroadcastSnapshot()
    {
        // Einmal für alle Empfänger; pro Empfänger kommen nur die eigenen Werte dazu (012: andere sehen fremde HP nicht).
        var players = _world.Players
            .Select(p => new PlayerState
            {
                Id = p.Id, Name = p.Name, X = (float)p.X, Y = (float)p.Y, Color = p.Color, Away = p.Away, SpawnSeq = p.SpawnSeq,
            })
            .ToList();

        foreach (var connection in _connections.Values)
        {
            var self = _world.GetPlayer(connection.PlayerId);
            var snapshot = new Snapshot
            {
                Tick = _tick,
                LastProcessedSeq = self?.LastProcessedSeq ?? 0,
                Hp = (float)(self?.Hp ?? 0),
                Mana = (float)(self?.Mana ?? 0),
            };
            snapshot.Players.AddRange(players);
            connection.Outbox.OfferSnapshot(new ServerMessage { Snapshot = snapshot }.ToByteArray());
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} ({Name}) beigetreten, {Count} online")]
    private partial void LogJoined(string playerId, string name, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} gegangen, {Count} online")]
    private partial void LogLeft(string playerId, int count);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} getrennt, Figur wartet, {Count} online")]
    private partial void LogAway(string playerId, int count);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Spieler {PlayerId} liest nicht mehr, Verbindung wird getrennt")]
    private partial void LogStalled(string playerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Spieler {PlayerId} auf anderem Gerät angemeldet, alte Verbindung beendet")]
    private partial void LogTakenOver(string playerId);
}
