using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Bestätigung ausgeführter Angriffe im GameLoop (013 AK-11, AK-12, AK-22): nur an den Angreifer, vor den
/// Kampfereignissen, nie für abgelehnte oder gesperrte Versuche.
/// </summary>
public sealed class GameLoopAttackAcceptedTests
{
    private const float DummyMaxHp = (float)GameSettings.DummyMaxHp;

    private static readonly float CooldownSeconds =
        (float)((double)GameSettings.ToTicks(GameSettings.GlobalCooldownSeconds) / GameSettings.TickRateHz);

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

    private static void SendAttack(PlayerConnection connection, Attack.Types.Kind kind, string targetId = Combat.DummyId) =>
        Assert.True(connection.Attacks.Writer.TryWrite(new Attack { Kind = kind, TargetId = targetId }));

    // Dreht die Figur nach oben zum Dummy und wartet den Nachlauf ab.
    private static void FaceDummy(GameLoop game, PlayerConnection connection)
    {
        Assert.True(connection.Inbox.Writer.TryWrite(new MoveInput(1, 0, -0.1f)));
        TickTimes(game, 1 + World.MaxCoastTicks + 1);
        Drain(connection);
    }

    private static TargetState Dummy(Snapshot snapshot) => snapshot.Targets.Single(t => t.Id == Combat.DummyId);

    [Fact] // AK-11, AK-22
    public void Melee_is_confirmed_to_the_attacker_only_with_the_cooldown()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", (500, 500));
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);
        Drain(observer);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        var messages = Drain(a);

        Assert.Equal([ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        Assert.Equal(CooldownSeconds, messages[0].AttackAccepted.CooldownSeconds);
        Assert.Equal(DummyMaxHp - (float)GameSettings.MeleeDamage, Dummy(LastSnapshot(messages)).Hp);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));
    }

    [Fact] // AK-22
    public void Ranged_is_confirmed_before_the_combat_events()
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

        Assert.Equal(
            [ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.CombatEvents, ServerMessage.PayloadOneofCase.Snapshot],
            Payloads(messages));
        Assert.Equal(CooldownSeconds, messages[0].AttackAccepted.CooldownSeconds);
        Assert.Equal([ServerMessage.PayloadOneofCase.CombatEvents, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));
    }

    [Fact] // AK-12, AK-22
    public void Rejected_attempt_is_not_confirmed_and_does_not_block()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", RangedSpot);
        game.Tick();
        FaceDummy(game, a);

        SendAttack(a, Attack.Types.Kind.Melee);
        SendAttack(a, Attack.Types.Kind.Ranged);
        game.Tick();
        var messages = Drain(a);

        Assert.Equal(
            [
                ServerMessage.PayloadOneofCase.AttackRejected,
                ServerMessage.PayloadOneofCase.AttackAccepted,
                ServerMessage.PayloadOneofCase.CombatEvents,
                ServerMessage.PayloadOneofCase.Snapshot,
            ],
            Payloads(messages));
        Assert.Equal(AttackRejected.Types.Reason.OutOfRange, messages[0].AttackRejected.Reason);
    }

    [Fact] // AK-11, AK-20, AK-22
    public void Attempts_during_the_cooldown_stay_silent()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);
        var cooldown = GameSettings.ToTicks(GameSettings.GlobalCooldownSeconds);

        SendAttack(a, Attack.Types.Kind.Melee);
        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        var first = Drain(a);
        Assert.Equal([ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.Snapshot], Payloads(first));
        var hp = Dummy(LastSnapshot(first)).Hp;

        TickTimes(game, cooldown - 2);
        Drain(a);
        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        var during = Drain(a);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(during));
        Assert.Equal(hp, Dummy(LastSnapshot(during)).Hp);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        Assert.Equal([ServerMessage.PayloadOneofCase.AttackAccepted, ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(a)));
    }
}
