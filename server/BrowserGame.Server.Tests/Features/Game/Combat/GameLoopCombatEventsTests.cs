using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Kampfereignisse im GameLoop (013 AK-21): Projektiltempo im Welcome, Start und Treffer an alle vor dem Snapshot
/// desselben Ticks, nichts für Nahkampf.
/// </summary>
public sealed class GameLoopCombatEventsTests
{
    private const float DummyMaxHp = (float)GameSettings.DummyMaxHp;
    private const float MaxMana = (float)GameSettings.MaxMana;

    // Nach dem Drehen nach oben rutscht die Figur 3 Einheiten: Abstand 25 (Nahkampf) bzw. 97 (Fernkampf).
    private static readonly (double X, double Y) MeleeSpot = (GameSettings.DummyX, GameSettings.DummyY + 28);
    private static readonly (double X, double Y) RangedSpot = (GameSettings.DummyX, GameSettings.DummyY + 100);

    private static GameLoop NewGame() => new(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static Snapshot LastSnapshot(List<ServerMessage> messages) =>
        messages.Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Snapshot).Snapshot;

    private static List<ServerMessage.PayloadOneofCase> Payloads(IEnumerable<ServerMessage> messages) =>
        [.. messages.Select(m => m.PayloadCase)];

    private static void TickTimes(GameLoop game, uint count)
    {
        for (var i = 0; i < count; i++)
        {
            Assert.Empty(game.Tick());
        }
    }

    private static void SendAttack(PlayerConnection connection, Attack.Types.Kind kind) =>
        Assert.True(connection.Attacks.Writer.TryWrite(new Attack { Kind = kind, TargetId = Combat.DummyId }));

    // Dreht die Figur nach oben zum Dummy und wartet den Nachlauf ab.
    private static void FaceDummy(GameLoop game, PlayerConnection connection)
    {
        Assert.True(connection.Inbox.Writer.TryWrite(new MoveInput(1, 0, -0.1f)));
        TickTimes(game, 1 + World.MaxCoastTicks + 1);
        Drain(connection);
    }

    private static TargetState Dummy(Snapshot snapshot) => snapshot.Targets.Single(t => t.Id == Combat.DummyId);

    [Fact] // AK-21
    public void Welcome_carries_projectile_speed()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        game.Tick();

        var welcome = Drain(a).First(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome;

        Assert.Equal((float)GameSettings.ProjectileSpeed, welcome.ProjectileSpeed);
    }

    [Fact] // AK-7, AK-14, AK-21
    public void Ranged_attack_reports_launch_and_hit_to_everyone_before_the_snapshot()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", (500, 500));
        var a = game.Join("a", "A", RangedSpot);
        game.Tick();
        FaceDummy(game, a);
        Drain(observer);

        SendAttack(a, Attack.Types.Kind.Ranged);
        game.Tick();
        var messages = Drain(a);
        var observed = Drain(observer);

        Assert.Equal(
            [ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.CombatEvents, ServerMessage.PayloadOneofCase.Snapshot],
            Payloads(messages));
        Assert.Equal([ServerMessage.PayloadOneofCase.CombatEvents, ServerMessage.PayloadOneofCase.Snapshot], Payloads(observed));
        var started = messages[1].CombatEvents;
        var launch = Assert.Single(started.Launched);
        Assert.Empty(started.Hits);
        Assert.NotEqual(0u, launch.ProjectileId);
        Assert.Equal("a", launch.ShooterId);
        Assert.Equal(Combat.DummyId, launch.TargetId);
        Assert.Equal(started, observed[0].CombatEvents);
        var snapshot = LastSnapshot(messages);
        Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
        Assert.Equal(MaxMana - (float)GameSettings.RangedManaCost, snapshot.Mana);

        var flying = 1;
        List<ServerMessage> tickMessages;
        do
        {
            Assert.True(flying <= 20);
            game.Tick();
            tickMessages = Drain(a);
            if (tickMessages.Any(m => m.PayloadCase == ServerMessage.PayloadOneofCase.CombatEvents))
            {
                break;
            }

            Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(tickMessages));
            Assert.Equal(DummyMaxHp, Dummy(LastSnapshot(tickMessages)).Hp);
            flying++;
        }
        while (true);

        Assert.True(flying >= 2);
        Assert.Equal([ServerMessage.PayloadOneofCase.CombatEvents, ServerMessage.PayloadOneofCase.Snapshot], Payloads(tickMessages));
        var landed = tickMessages[0].CombatEvents;
        Assert.Empty(landed.Launched);
        var hit = Assert.Single(landed.Hits);
        Assert.Equal(launch.ProjectileId, hit.ProjectileId);
        Assert.Equal(Combat.DummyId, hit.TargetId);
        Assert.Equal((float)GameSettings.RangedDamage, hit.Damage);
        Assert.Equal(DummyMaxHp - hit.Damage, Dummy(LastSnapshot(tickMessages)).Hp);
        Assert.Equal(landed, Drain(observer).Last(m => m.PayloadCase == ServerMessage.PayloadOneofCase.CombatEvents).CombatEvents);
    }

    [Fact] // AK-4, AK-21
    public void Melee_sends_no_combat_events()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", (500, 500));
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);
        Drain(observer);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();

        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));
        Assert.Equal([ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(a)));
    }
}
