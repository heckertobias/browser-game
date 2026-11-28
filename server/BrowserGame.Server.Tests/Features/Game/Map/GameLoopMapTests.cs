using BrowserGame.Server.Features.Game;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using GroundKind = BrowserGame.Contracts.V1.GroundKind;
using ServerMessage = BrowserGame.Contracts.V1.ServerMessage;
using Welcome = BrowserGame.Contracts.V1.Welcome;

namespace BrowserGame.Server.Tests;

/// <summary>Die Karte im Welcome ohne Netz (018 AK-3, AK-6): Nachricht aus der Karte bauen, jedes Welcome trägt dieselbe.</summary>
public sealed class GameLoopMapTests
{
    private const GroundKind M = GroundKind.Mountain;
    private const GroundKind W = GroundKind.Water;
    private const GroundKind P = GroundKind.Path;
    private const GroundKind G = GroundKind.Grass;

    private static AreaMap Small() => new(16, 4, 3, -1, -2, [M, M, M, M, M, G, P, M, M, W, G, M]);

    private static List<ServerMessage> Drain(PlayerConnection connection)
    {
        var messages = new List<ServerMessage>();
        while (connection.Outbox.Reader.TryRead(out var bytes))
        {
            messages.Add(ServerMessage.Parser.ParseFrom(bytes));
        }

        return messages;
    }

    private static Welcome WelcomeOf(PlayerConnection connection) =>
        Drain(connection).First(m => m.PayloadCase == ServerMessage.PayloadOneofCase.Welcome).Welcome;

    [Fact]
    public void Area_message_carries_the_map()
    {
        var area = GameLoop.AreaMessage(Small());

        Assert.Equal(16u, area.TileSize);
        Assert.Equal(4u, area.Width);
        Assert.Equal(3u, area.Height);
        Assert.Equal(-1, area.OriginTx);
        Assert.Equal(-2, area.OriginTy);
        Assert.Equal(TestMaps.SmallGround, area.Ground.ToByteArray());
    }

    [Fact]
    public void Every_welcome_carries_the_same_area()
    {
        using var game = new GameLoop(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build(), map: Small());
        var a = game.Join("a", "A", null);
        var b = game.Join("b", "B", null);
        game.Tick();

        var welcomeA = WelcomeOf(a);
        var welcomeB = WelcomeOf(b);

        Assert.Equal(GameLoop.AreaMessage(Small()), welcomeA.Area);
        Assert.Equal(welcomeA.Area, welcomeB.Area);
    }

    [Fact]
    public void Welcome_without_map_has_no_area()
    {
        using var game = new GameLoop(NullLogger<GameLoop>.Instance, new ConfigurationBuilder().Build());
        var a = game.Join("a", "A", null);
        game.Tick();

        Assert.Null(WelcomeOf(a).Area);
    }
}
