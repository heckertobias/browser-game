using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Ausgang einer Verbindung, gelesen von genau einer Sendeschleife. Pflichtnachrichten gehen in Reihenfolge und immer zuerst
/// raus. Von den Snapshots wartet nur der neueste: Jeder enthält den ganzen Stand, ein ausgelassener verliert nichts.
/// Erreichen die Pflichtnachrichten <paramref name="requiredLimit"/>, liest der Client nicht mehr; dann endet die Verbindung
/// über <paramref name="onOverflow"/>, statt still zu verwerfen.
/// </summary>
internal sealed class PlayerOutbox(int requiredLimit, Action? onOverflow = null) : ChannelReader<byte[]>
{
    // Ein Lock über beide Teile: Wer einen Snapshot liest, sieht alle vorher geschriebenen Pflichtnachrichten.
    // So überholt kein Snapshot Welcome und folgt keiner auf Kicked.
    private readonly Lock _gate = new();
    private readonly Queue<byte[]> _required = new();
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

    // Ein Platz: Ein abgelegtes Signal geht nicht verloren, ein überzähliges kostet nur eine Prüfung.
    private readonly Channel<bool> _wake = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite, SingleReader = true });

    private byte[]? _snapshot;
    private bool _closed;

    public ChannelReader<byte[]> Reader => this;

    public override Task Completion => _completion.Task;

    public void SendRequired(byte[] message)
    {
        bool overflowed;
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            overflowed = _required.Count >= requiredLimit;
            if (overflowed)
            {
                _required.Clear();
                _snapshot = null;
                _closed = true;
                _completion.TrySetResult();
            }
            else
            {
                _required.Enqueue(message);
            }
        }

        _wake.Writer.TryWrite(true);
        if (overflowed)
        {
            onOverflow?.Invoke();
        }
    }

    public void OfferSnapshot(byte[] snapshot)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _snapshot = snapshot;
        }

        _wake.Writer.TryWrite(true);
    }

    /// <summary>
    /// Beendet den Ausgang: Wartende Pflichtnachrichten gehen noch raus, danach <paramref name="lastMessage"/>; kein Snapshot mehr.
    /// In einem Schritt, sonst könnte der Leser nach <paramref name="lastMessage"/> noch den wartenden Snapshot nehmen.
    /// </summary>
    public void Close(byte[]? lastMessage = null)
    {
        lock (_gate)
        {
            if (_closed)
            {
                return;
            }

            _closed = true;
            _snapshot = null;
            if (lastMessage is not null)
            {
                _required.Enqueue(lastMessage);
            }

            if (_required.Count == 0)
            {
                _completion.TrySetResult();
            }
        }

        _wake.Writer.TryWrite(true);
    }

    public override bool TryRead([MaybeNullWhen(false)] out byte[] item)
    {
        lock (_gate)
        {
            if (_required.TryDequeue(out item))
            {
                if (_closed && _required.Count == 0)
                {
                    _completion.TrySetResult();
                }

                return true;
            }

            item = _snapshot;
            _snapshot = null;
            return item is not null;
        }
    }

    public override async ValueTask<bool> WaitToReadAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            lock (_gate)
            {
                if (_required.Count > 0 || _snapshot is not null)
                {
                    return true;
                }

                if (_closed)
                {
                    return false;
                }
            }

            await _wake.Reader.ReadAsync(cancellationToken);
        }
    }
}
