using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class PlayerOutboxTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(10);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static byte[] Msg(byte id) => [id];

    private static PlayerOutbox NewOutbox(int limit = 1024) => new(limit);

    private static List<byte[]> Drain(PlayerOutbox outbox)
    {
        var items = new List<byte[]>();
        while (outbox.TryRead(out var item))
        {
            items.Add(item);
        }

        return items;
    }

    [Fact]
    public void Required_goes_before_waiting_snapshot()
    {
        var outbox = NewOutbox();
        byte[] a = Msg(1), b = Msg(2), s = Msg(3);

        outbox.OfferSnapshot(s);
        outbox.SendRequired(a);
        outbox.SendRequired(b);

        var items = Drain(outbox);
        Assert.Equal(3, items.Count);
        Assert.Same(a, items[0]);
        Assert.Same(b, items[1]);
        Assert.Same(s, items[2]);
    }

    [Fact]
    public async Task Required_overtakes_waiting_snapshot_while_reading()
    {
        var outbox = NewOutbox();
        byte[] a = Msg(1), b = Msg(2), s = Msg(3);
        await using var reader = outbox.ReadAllAsync(Ct).GetAsyncEnumerator(Ct);

        outbox.OfferSnapshot(s);
        outbox.SendRequired(a);
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(Limit, Ct));
        Assert.Same(a, reader.Current);

        outbox.SendRequired(b);
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(Limit, Ct));
        Assert.Same(b, reader.Current);
        Assert.True(await reader.MoveNextAsync().AsTask().WaitAsync(Limit, Ct));
        Assert.Same(s, reader.Current);
    }

    [Fact]
    public void New_snapshot_replaces_waiting_snapshot()
    {
        var outbox = NewOutbox();
        byte[] first = Msg(1), second = Msg(2);

        outbox.OfferSnapshot(first);
        outbox.OfferSnapshot(second);

        Assert.Same(second, Assert.Single(Drain(outbox)));
    }

    [Fact]
    public void Required_keeps_its_order()
    {
        var outbox = NewOutbox();
        var written = Enumerable.Range(0, 500).Select(i => BitConverter.GetBytes(i)).ToList();

        foreach (var message in written)
        {
            outbox.SendRequired(message);
        }

        Assert.Equal(written, Drain(outbox));
    }

    [Fact]
    public void Close_with_last_message_sends_it_last_and_drops_snapshot()
    {
        var outbox = NewOutbox();
        byte[] a = Msg(1), s = Msg(2), k = Msg(3);

        outbox.SendRequired(a);
        outbox.OfferSnapshot(s);
        outbox.Close(k);

        var items = Drain(outbox);
        Assert.Equal(2, items.Count);
        Assert.Same(a, items[0]);
        Assert.Same(k, items[1]);
        Assert.False(outbox.TryRead(out _));
        Assert.True(outbox.Completion.IsCompleted);
    }

    [Fact]
    public void Writes_after_close_are_ignored()
    {
        var calls = 0;
        var outbox = new PlayerOutbox(1, () => calls++);
        byte[] k = Msg(1);
        outbox.Close(k);

        outbox.SendRequired(Msg(2));
        outbox.OfferSnapshot(Msg(3));
        outbox.Close(Msg(4));

        Assert.Same(k, Assert.Single(Drain(outbox)));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Close_without_message_keeps_required_and_completes_when_drained()
    {
        var outbox = NewOutbox();
        byte[] a = Msg(1);
        outbox.SendRequired(a);
        outbox.OfferSnapshot(Msg(2));

        outbox.Close();

        Assert.False(outbox.Completion.IsCompleted);
        Assert.True(outbox.TryRead(out var item));
        Assert.Same(a, item);
        Assert.True(outbox.Completion.IsCompleted);
        Assert.False(outbox.TryRead(out _));
    }

    [Fact]
    public async Task Waiting_reader_wakes_on_message()
    {
        var outbox = NewOutbox();
        var waiting = outbox.WaitToReadAsync(Ct).AsTask();
        Assert.False(waiting.IsCompleted);
        outbox.SendRequired(Msg(1));
        Assert.True(await waiting.WaitAsync(Limit, Ct));
        Drain(outbox);

        waiting = outbox.WaitToReadAsync(Ct).AsTask();
        Assert.False(waiting.IsCompleted);
        outbox.OfferSnapshot(Msg(2));
        Assert.True(await waiting.WaitAsync(Limit, Ct));
    }

    [Fact]
    public async Task Waiting_reader_ends_on_close()
    {
        var outbox = NewOutbox();
        var waiting = outbox.WaitToReadAsync(Ct).AsTask();
        Assert.False(waiting.IsCompleted);

        outbox.Close();

        Assert.False(await waiting.WaitAsync(Limit, Ct));
        Assert.Empty(await outbox.ReadAllAsync(Ct).ToListAsync(Ct).AsTask().WaitAsync(Limit, Ct));
        Assert.True(outbox.Completion.IsCompleted);
    }

    [Fact]
    public async Task Waiting_reader_can_be_cancelled()
    {
        var outbox = NewOutbox();
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        var waiting = outbox.WaitToReadAsync(cts.Token).AsTask();
        Assert.False(waiting.IsCompleted);

        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(Limit, Ct));

        outbox.SendRequired(Msg(1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await outbox.WaitToReadAsync(cts.Token));
    }

    [Fact]
    public void Overflow_closes_and_reports_once()
    {
        var calls = 0;
        var outbox = new PlayerOutbox(2, () => calls++);
        outbox.SendRequired(Msg(1));
        outbox.SendRequired(Msg(2));
        Assert.Equal(0, calls);

        outbox.SendRequired(Msg(3));

        Assert.Equal(1, calls);
        Assert.True(outbox.Completion.IsCompleted);
        Assert.False(outbox.TryRead(out _));

        outbox.SendRequired(Msg(4));
        outbox.OfferSnapshot(Msg(5));
        Assert.Equal(1, calls);
        Assert.False(outbox.TryRead(out _));
    }

    [Fact]
    public void Close_ignores_limit_for_last_message()
    {
        var calls = 0;
        var outbox = new PlayerOutbox(1, () => calls++);
        outbox.SendRequired(Msg(1));
        byte[] k = Msg(2);

        outbox.Close(k);

        Assert.Equal(0, calls);
        var items = Drain(outbox);
        Assert.Equal(2, items.Count);
        Assert.Same(k, items[1]);
    }

    [Fact]
    public async Task Concurrent_reader_never_sees_snapshot_before_required_or_after_last()
    {
        for (var run = 0; run < 2000; run++)
        {
            var outbox = NewOutbox();
            byte[] w = Msg(1), s = Msg(2), k = Msg(3);
            using var start = new Barrier(2);
            var reading = Task.Run(async () =>
            {
                start.SignalAndWait(Ct);
                return await outbox.ReadAllAsync(Ct).ToListAsync(Ct);
            }, Ct);
            start.SignalAndWait(Ct);

            outbox.SendRequired(w);
            outbox.OfferSnapshot(s);
            outbox.Close(k);

            var items = await reading.WaitAsync(Limit, Ct);
            Assert.Same(w, items[0]);
            Assert.Same(k, items[^1]);
            Assert.InRange(items.Count, 2, 3);
        }
    }

    [Fact]
    public async Task Spinning_reader_never_gets_snapshot_before_required()
    {
        for (var run = 0; run < 20000; run++)
        {
            var outbox = NewOutbox();
            byte[] w = Msg(1), s = Msg(2);
            using var start = new Barrier(2);
            var reading = Task.Run(() =>
            {
                start.SignalAndWait(Ct);
                byte[]? first;
                while (!outbox.TryRead(out first))
                {
                }

                return first;
            }, Ct);
            start.SignalAndWait(Ct);

            outbox.SendRequired(w);
            outbox.OfferSnapshot(s);

            Assert.Same(w, await reading.WaitAsync(Limit, Ct));
        }
    }

    [Fact]
    public async Task Blocked_reader_wakes_for_every_write()
    {
        var outbox = NewOutbox();
        using var received = new SemaphoreSlim(0);
        var reading = Task.Run(async () =>
        {
            await foreach (var _ in outbox.ReadAllAsync(Ct))
            {
                received.Release();
            }
        }, Ct);

        for (var run = 0; run < 50000; run++)
        {
            if (run % 2 == 0)
            {
                outbox.SendRequired(Msg(1));
            }
            else
            {
                outbox.OfferSnapshot(Msg(2));
            }

            Assert.True(await received.WaitAsync(Limit, Ct));
        }

        outbox.Close();
        await reading.WaitAsync(Limit, Ct);
    }
}
