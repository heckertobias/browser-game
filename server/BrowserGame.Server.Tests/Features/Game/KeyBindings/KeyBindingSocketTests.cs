using System.Net.WebSockets;
using BrowserGame.Contracts.V1;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Google.Protobuf;
using Microsoft.EntityFrameworkCore;
using ProtoAction = BrowserGame.Contracts.V1.Action;

namespace BrowserGame.Server.Tests;

public sealed class KeyBindingSocketTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static KeyBinding Bind(ProtoAction action, params string[] codes)
    {
        var binding = new KeyBinding { Action = action };
        binding.Codes.AddRange(codes);
        return binding;
    }

    private static KeyBindings Bindings(params KeyBinding[] bindings)
    {
        var result = new KeyBindings();
        result.Bindings.AddRange(bindings);
        return result;
    }

    // Gültig und ungleich Standard: Pfeil hoch zusätzlich auf "Nach oben".
    private static KeyBindings Custom() => Bindings(
        Bind(ProtoAction.MoveUp, "KeyW", "ArrowUp"),
        Bind(ProtoAction.MoveDown, "KeyS"),
        Bind(ProtoAction.MoveLeft, "KeyA"),
        Bind(ProtoAction.MoveRight, "KeyD"));

    // Welcome ohne Feld 5 zählt wie leer.
    private static KeyBindings Of(Welcome welcome) => welcome.KeyBindings ?? new KeyBindings();

    private static Task SendSaveAsync(WebSocket socket, KeyBindings? bindings, CancellationToken ct) =>
        socket.SendAsync(
            new ClientMessage { SaveKeyBindings = new SaveKeyBindings { KeyBindings = bindings } }.ToByteArray(),
            WebSocketMessageType.Binary, true, ct);

    // Der Server liest Nachrichten einer Verbindung der Reihe nach und wartet das Speichern ab.
    // Ist eine danach geschickte Eingabe verarbeitet, ist alles davor erledigt.
    private static async Task ProcessedAsync(WebSocket socket, uint seq, CancellationToken ct)
    {
        await GameSockets.SendInputAsync(socket, seq, 0, 0, ct);
        await GameSockets.ReceiveSnapshotUntilAsync(socket, s => s.LastProcessedSeq >= seq, ct);
    }

    private async Task<byte[]?> StoredAsync(string accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return (await db.Users.AsNoTracking().SingleAsync(a => a.Id == accountId, Ct)).KeyBindings;
    }

    private async Task<(WebSocket Socket, Welcome Welcome)> ConnectOtherDeviceAsync(string email)
    {
        var socket = await GameSockets.ConnectAsync(factory, await GameSockets.SignInCookieAsync(factory, email), GameSockets.Origin, Ct);
        return (socket, await GameSockets.ReceiveWelcomeAsync(socket, Ct));
    }

    [Fact] // AK-10
    public async Task New_account_gets_empty_key_bindings_in_welcome()
    {
        var (a, id, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = a;
        var welcome = await GameSockets.ReceiveWelcomeAsync(a, Ct);

        Assert.Equal(new KeyBindings(), Of(welcome));
        Assert.Null(await StoredAsync(id));
    }

    [Fact] // AK-9
    public async Task Saved_key_bindings_reach_welcome_on_other_device()
    {
        var (a, id, account) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = a;
        await GameSockets.ReceiveWelcomeAsync(a, Ct);

        await SendSaveAsync(a, Custom(), Ct);
        await ProcessedAsync(a, 1, Ct);

        Assert.Equal(Custom(), KeyBindingRules.FromStored(await StoredAsync(id)));
        var (b, welcome) = await ConnectOtherDeviceAsync(account.Email);
        using var __ = b;
        Assert.Equal(Custom(), Of(welcome));
    }

    [Theory] // AK-10
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reset_stores_null_and_sends_empty_welcome(bool explicitEmpty)
    {
        var (a, id, account) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = a;
        await GameSockets.ReceiveWelcomeAsync(a, Ct);
        await SendSaveAsync(a, Custom(), Ct);
        await ProcessedAsync(a, 1, Ct);
        Assert.NotNull(await StoredAsync(id));

        await SendSaveAsync(a, explicitEmpty ? new KeyBindings() : null, Ct);
        await ProcessedAsync(a, 2, Ct);

        Assert.Null(await StoredAsync(id));
        var (b, welcome) = await ConnectOtherDeviceAsync(account.Email);
        using var __ = b;
        Assert.Equal(new KeyBindings(), Of(welcome));
    }

    [Fact] // AK-9
    public async Task Invalid_key_bindings_are_not_stored_and_connection_stays()
    {
        var (a, id, account) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var _ = a;
        await GameSockets.ReceiveWelcomeAsync(a, Ct);
        await SendSaveAsync(a, Custom(), Ct);
        await ProcessedAsync(a, 1, Ct);

        await SendSaveAsync(a, Bindings(
            Bind(ProtoAction.MoveUp, "Escape"),
            Bind(ProtoAction.MoveDown, "KeyS"),
            Bind(ProtoAction.MoveLeft, "KeyA"),
            Bind(ProtoAction.MoveRight, "KeyD")), Ct);
        await ProcessedAsync(a, 2, Ct);

        Assert.Equal(Custom(), KeyBindingRules.FromStored(await StoredAsync(id)));
        var (b, welcome) = await ConnectOtherDeviceAsync(account.Email);
        using var __ = b;
        Assert.Equal(Custom(), Of(welcome));
    }

    [Fact] // AK-9
    public async Task Corrupt_stored_key_bindings_give_empty_welcome()
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        var id = await GameSockets.AccountIdAsync(factory, account.Email);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            await db.Users.Where(a => a.Id == id)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.KeyBindings, new byte[] { 0x0A, 0x05 }), Ct);
        }

        var (socket, welcome) = await ConnectOtherDeviceAsync(account.Email);
        using var _ = socket;

        Assert.Equal(new KeyBindings(), Of(welcome));
        await ProcessedAsync(socket, 1, Ct);
        Assert.Equal(new byte[] { 0x0A, 0x05 }, await StoredAsync(id));
    }

    [Fact] // AK-9
    public async Task Store_does_not_create_unknown_account()
    {
        await factory.Services.GetRequiredService<KeyBindingStore>().SaveAsync("nie-angelegt", [1, 2], Ct);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        Assert.False(await db.Users.AnyAsync(a => a.Id == "nie-angelegt", Ct));
    }
}
