using System.Diagnostics.CodeAnalysis;
using System.Threading.Channels;

namespace BrowserGame.Server.Features.Game;

/// <summary>
/// Angenommene Nachrichten auf dem Weg vom Tick-Thread zum <see cref="ChatStore"/>. <see cref="Add"/> blockiert nie.
/// Hängt die Datenbank, fallen die ältesten weg: Für den Verlauf (011 AK-15) zählen die neuesten.
/// </summary>
internal sealed class ChatSaveQueue
{
    private readonly Channel<ChatHistoryEntry> _channel;
    private long _dropped;

    public ChatSaveQueue(int capacity) => _channel = Channel.CreateBounded<ChatHistoryEntry>(
        new BoundedChannelOptions(capacity) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true, SingleWriter = true },
        _ => Interlocked.Increment(ref _dropped));

    public void Add(ChatHistoryEntry entry) => _channel.Writer.TryWrite(entry);

    public bool TryRead([MaybeNullWhen(false)] out ChatHistoryEntry entry) => _channel.Reader.TryRead(out entry);

    /// <summary>Liefert die Zahl der seit dem letzten Aufruf verworfenen Nachrichten.</summary>
    public long TakeDroppedCount() => Interlocked.Exchange(ref _dropped, 0);
}
