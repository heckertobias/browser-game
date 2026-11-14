using BrowserGame.Contracts.V1;
using BrowserGame.Server.Features.Game;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace BrowserGame.Server.Tests;

public sealed class GameLoopCombatTests
{
    private const float DummyX = (float)GameSettings.DummyX;
    private const float DummyY = (float)GameSettings.DummyY;
    private const float DummyMaxHp = (float)GameSettings.DummyMaxHp;
    private const float MaxMana = (float)GameSettings.MaxMana;
    private const float RegenPerTick = (float)(GameSettings.ManaRegenPerSecond / GameSettings.TickRateHz);
    private const float DefaultFacing = (float)GameSettings.DefaultFacing;

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

    private static Snapshot LastSnapshot(PlayerConnection connection) => LastSnapshot(Drain(connection));

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

    [Fact] // AK-13
    public void First_snapshot_contains_the_dummy()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        game.Tick();

        var snapshot = LastSnapshot(a);

        var dummy = Assert.Single(snapshot.Targets);
        Assert.Equal(Combat.DummyId, dummy.Id);
        Assert.Equal(TargetState.Types.Kind.TrainingDummy, dummy.Kind);
        Assert.Equal(DummyX, dummy.X);
        Assert.Equal(DummyY, dummy.Y);
        Assert.Equal(DummyMaxHp, dummy.Hp);
        Assert.Equal(DummyMaxHp, dummy.MaxHp);
        Assert.Empty(snapshot.Projectiles);
    }

    [Fact] // AK-13
    public void Dummy_is_the_same_for_every_receiver()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", null);
        var b = game.Join("b", "B", null);
        game.Tick();

        var dummyA = Dummy(LastSnapshot(a));
        var dummyB = Dummy(LastSnapshot(b));

        Assert.Equal(dummyA, dummyB);
    }

    [Fact] // AK-13 (Verdrahtung AK-4, AK-6)
    public void Melee_from_the_channel_hits_in_the_same_tick()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        var messages = Drain(a);

        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        var snapshot = LastSnapshot(messages);
        Assert.Equal(DummyMaxHp - (float)GameSettings.MeleeDamage, Dummy(snapshot).Hp);
        Assert.Equal(MaxMana, snapshot.Mana);
    }

    [Fact] // AK-7
    public void Ranged_projectile_shows_in_snapshots_and_hits_on_impact()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", RangedSpot);
        game.Tick();
        FaceDummy(game, a);
        game.Tick();
        var playerY = LastSnapshot(a).Players.Single(p => p.Id == "a").Y;

        SendAttack(a, Attack.Types.Kind.Ranged);
        game.Tick();
        var snapshot = LastSnapshot(a);

        var projectile = Assert.Single(snapshot.Projectiles);
        Assert.NotEqual(0u, projectile.Id);
        Assert.Equal(DummyX, projectile.X);
        Assert.InRange(projectile.Y, DummyY + 0.001f, playerY - 0.001f);
        Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
        Assert.Equal(MaxMana - (float)GameSettings.RangedManaCost, snapshot.Mana);

        var flying = 1;
        var previousY = projectile.Y;
        for (var i = 0; i < 20 && snapshot.Projectiles.Count > 0; i++)
        {
            game.Tick();
            snapshot = LastSnapshot(a);
            if (snapshot.Projectiles.Count == 0)
            {
                break;
            }

            var next = Assert.Single(snapshot.Projectiles);
            Assert.Equal(projectile.Id, next.Id);
            Assert.True(next.Y < previousY);
            Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
            previousY = next.Y;
            flying++;
        }

        Assert.True(flying >= 2);
        Assert.Empty(snapshot.Projectiles);
        Assert.Equal(DummyMaxHp - (float)GameSettings.RangedDamage, Dummy(snapshot).Hp);
    }

    [Fact] // AK-8
    public void Not_enough_mana_is_reported_to_the_attacker_only()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", (500, 500));
        var a = game.Join("a", "A", RangedSpot);
        game.Tick();
        FaceDummy(game, a);
        game.SetVitals("a", mana: 0);
        game.Tick();
        Drain(observer);
        var before = LastSnapshot(a).Mana;

        SendAttack(a, Attack.Types.Kind.Ranged);
        game.Tick();
        var messages = Drain(a);

        Assert.Equal([ServerMessage.PayloadOneofCase.AttackRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        Assert.Equal(AttackRejected.Types.Reason.NotEnoughMana, messages[0].AttackRejected.Reason);
        var snapshot = LastSnapshot(messages);
        Assert.Empty(snapshot.Projectiles);
        Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
        Assert.Equal(before + RegenPerTick, snapshot.Mana);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));
    }

    [Fact] // AK-19
    public void Not_facing_is_reported_to_the_attacker_only()
    {
        using var game = NewGame();
        var observer = game.Join("obs", "Obs", (500, 500));
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        Drain(observer);
        Drain(a);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();
        var messages = Drain(a);

        Assert.Equal([ServerMessage.PayloadOneofCase.AttackRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        Assert.Equal(AttackRejected.Types.Reason.NotFacing, messages[0].AttackRejected.Reason);
        var snapshot = LastSnapshot(messages);
        Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
        Assert.Equal(DefaultFacing, snapshot.Players.Single(p => p.Id == "a").Facing);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));

        SendAttack(a, Attack.Types.Kind.Ranged);
        game.Tick();
        messages = Drain(a);

        Assert.Equal([ServerMessage.PayloadOneofCase.AttackRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        Assert.Equal(AttackRejected.Types.Reason.NotFacing, messages[0].AttackRejected.Reason);
        snapshot = LastSnapshot(messages);
        Assert.Empty(snapshot.Projectiles);
        Assert.Equal(MaxMana, snapshot.Mana);
    }

    [Fact] // AK-2, AK-11, AK-20
    public void Other_failures_stay_silent()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);

        SendAttack(a, Attack.Types.Kind.Unspecified);
        SendAttack(a, Attack.Types.Kind.Melee, "nope");
        SendAttack(a, Attack.Types.Kind.Melee);
        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();

        var messagesA = Drain(a);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(messagesA));
        Assert.Equal(DummyMaxHp - (float)GameSettings.MeleeDamage, Dummy(LastSnapshot(messagesA)).Hp);
    }

    [Fact] // AK-13
    public void Dead_dummy_is_missing_until_it_respawns()
    {
        using var game = NewGame();
        var a = game.Join("a", "A", MeleeSpot);
        game.Tick();
        FaceDummy(game, a);
        var hits = (int)Math.Ceiling(GameSettings.DummyMaxHp / GameSettings.MeleeDamage);
        var cooldown = GameSettings.ToTicks(GameSettings.GlobalCooldownSeconds);
        var respawn = GameSettings.ToTicks(GameSettings.DummyRespawnSeconds);

        for (var hit = 1; hit <= hits; hit++)
        {
            SendAttack(a, Attack.Types.Kind.Melee);
            game.Tick();
            var snapshot = LastSnapshot(a);
            if (hit < hits)
            {
                Assert.Equal(
                    (float)Math.Max(0, GameSettings.DummyMaxHp - hit * GameSettings.MeleeDamage), Dummy(snapshot).Hp);
                TickTimes(game, cooldown - 1);
            }
            else
            {
                Assert.Empty(snapshot.Targets);
            }
        }

        TickTimes(game, respawn - 1);
        Assert.Empty(LastSnapshot(a).Targets);

        game.Tick();

        Assert.Equal(DummyMaxHp, Dummy(LastSnapshot(a)).Hp);
    }

    [Fact] // AK-5, AK-20
    public void Out_of_range_melee_is_reported_to_the_attacker_only()
    {
        using var game = NewGame();
        var observer = game.Join("o", "O", (500, 500));
        var a = game.Join("a", "A", RangedSpot);
        game.Tick();
        FaceDummy(game, a);
        Drain(observer);

        SendAttack(a, Attack.Types.Kind.Melee);
        game.Tick();

        var messagesA = Drain(a);
        Assert.Equal([ServerMessage.PayloadOneofCase.AttackRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messagesA));
        Assert.Equal(AttackRejected.Types.Reason.OutOfRange, messagesA[0].AttackRejected.Reason);
        Assert.Equal(DummyMaxHp, Dummy(LastSnapshot(messagesA)).Hp);
        Assert.Equal([ServerMessage.PayloadOneofCase.Snapshot], Payloads(Drain(observer)));
    }

    [Fact] // AK-9, AK-20
    public void Out_of_range_ranged_is_reported_without_projectile_or_mana()
    {
        using var game = NewGame();
        var b = game.Join("b", "B", (GameSettings.DummyX, GameSettings.DummyY + 200));
        game.Tick();
        Drain(b);

        SendAttack(b, Attack.Types.Kind.Ranged);
        game.Tick();

        var messages = Drain(b);
        Assert.Equal([ServerMessage.PayloadOneofCase.AttackRejected, ServerMessage.PayloadOneofCase.Snapshot], Payloads(messages));
        Assert.Equal(AttackRejected.Types.Reason.OutOfRange, messages[0].AttackRejected.Reason);
        var snapshot = LastSnapshot(messages);
        Assert.Empty(snapshot.Projectiles);
        Assert.Equal(MaxMana, snapshot.Mana);
        Assert.Equal(DummyMaxHp, Dummy(snapshot).Hp);
    }
}
