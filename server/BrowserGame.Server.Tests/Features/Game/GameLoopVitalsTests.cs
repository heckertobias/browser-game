using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public class GameLoopVitalsTests
{
    private const uint LingerTicks = 20; // Game:LingerSeconds = 1 bei 20 Hz
    private const float MaxHp = (float)GameSettings.MaxHp;
    private const float MaxMana = (float)GameSettings.MaxMana;
    private const float RegenPerTick = (float)(GameSettings.ManaRegenPerSecond / GameSettings.TickRateHz);

    private static GameLoop NewGame() => new(
        NullLogger<GameLoop>.Instance,
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GameSettings.LingerSecondsKey] = "1" })
            .Build());

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static Snapshot LastSnapshot(PlayerConnection connection) =>
        Drain(connection).Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;

    private static void TickTimes(GameLoop game, uint count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.Empty(game.Tick());
        }
    }

    [Fact] // AK-2, AK-5
    public void Welcome_carries_max_values()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();

        var welcome = Drain(a).Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome;

        Assert.Equal(MaxHp, welcome.MaxHp);
        Assert.Equal(MaxMana, welcome.MaxMana);
    }

    [Fact] // AK-2, AK-5
    public void New_figure_starts_full_in_snapshot()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();

        var snapshot = LastSnapshot(a);

        Assert.Equal(MaxHp, snapshot.Hp);
        Assert.Equal(MaxMana, snapshot.Mana);
        Assert.Equal(0u, snapshot.Players.Single(p => p.Id == "acc").SpawnSeq);
    }

    [Fact] // AK-5, AK-6
    public void Snapshot_carries_own_values_per_receiver()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        var b = game.Join("b", "B", null);
        game.Tick();

        game.SetVitals("a", hp: 30, mana: 20);
        game.Tick();

        var seenByA = LastSnapshot(a);
        Assert.Equal(30f, seenByA.Hp);
        Assert.Equal(20f + RegenPerTick, seenByA.Mana);
        var seenByB = LastSnapshot(b);
        Assert.Equal(MaxHp, seenByB.Hp);
        Assert.Equal(MaxMana, seenByB.Mana);
    }

    [Fact] // AK-6
    public void Mana_regeneration_reaches_snapshot()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        game.Tick();
        game.SetVitals("a", mana: 0);
        game.Tick();
        Drain(a);

        game.Tick();

        var snapshot = LastSnapshot(a);
        Assert.Equal(2 * RegenPerTick, snapshot.Mana);
        Assert.Equal(MaxHp, snapshot.Hp);
    }

    [Fact] // AK-6, AK-8
    public void Death_respawns_with_full_values_and_new_spawn_seq()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", null);
        var a = game.Join("acc", "A", (500, 500));
        game.Tick();

        game.SetVitals("acc", hp: 0);
        game.Tick();

        var figure = LastSnapshot(observer).Players.Single(p => p.Id == "acc");
        Assert.Equal(1u, figure.SpawnSeq);
        Assert.True(Math.Sqrt((figure.X * figure.X) + (figure.Y * figure.Y)) <= World.SpawnRadius + 1e-3);
        var own = LastSnapshot(a);
        Assert.Equal(MaxHp, own.Hp);
        Assert.Equal(MaxMana, own.Mana);
    }

    [Fact] // AK-2
    public void Takeover_by_other_device_keeps_vitals()
    {
        using var game = NewGame();
        game.Join("acc", "A", null);
        game.Tick();
        game.SetVitals("acc", hp: 40, mana: 10);
        game.Tick();

        var b = game.Join("acc", "A", null);
        game.Tick();

        var messages = Drain(b);
        Assert.Equal(MaxHp, messages.Single(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome.MaxHp);
        var snapshot = messages.Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;
        Assert.Equal(40f, snapshot.Hp);
        Assert.True(snapshot.Mana < MaxMana);
    }

    [Fact] // AK-2
    public void Rejoin_within_linger_keeps_vitals()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", null);
        game.Tick();
        game.SetVitals("acc", hp: 40, mana: 10);
        game.Tick();
        game.Leave(a);
        TickTimes(game, 5);

        var b = game.Join("acc", "A", null);
        game.Tick();

        var snapshot = LastSnapshot(b);
        Assert.Equal(40f, snapshot.Hp);
        Assert.True(snapshot.Mana < MaxMana);
    }

    [Fact] // AK-2
    public void Rejoin_after_linger_starts_full()
    {
        using var game = NewGame();
        var a = game.Join("acc", "A", (1, 2));
        game.Tick();
        game.SetVitals("acc", hp: 40, mana: 10);
        game.Tick();
        game.Leave(a);
        TickTimes(game, LingerTicks);
        Assert.Single(game.Tick());

        var b = game.Join("acc", "A", (1, 2));
        game.Tick();

        var snapshot = LastSnapshot(b);
        Assert.Equal(MaxHp, snapshot.Hp);
        Assert.Equal(MaxMana, snapshot.Mana);
    }

    [Fact] // AK-10
    public void Set_vitals_of_unknown_account_is_ignored()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        game.Tick();

        game.SetVitals("fehlt", hp: 0);
        game.Tick();

        var snapshot = LastSnapshot(a);
        Assert.Equal(MaxHp, snapshot.Hp);
        Assert.Equal(0u, snapshot.Players.Single(p => p.Id == "a").SpawnSeq);
    }
}
