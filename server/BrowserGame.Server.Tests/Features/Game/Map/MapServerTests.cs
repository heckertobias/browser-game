using BrowserGame.Server.Features.Game;
using Microsoft.AspNetCore.Hosting;
using ServerMessage = BrowserGame.Contracts.V1.ServerMessage;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Die Karte über den ganzen Server (018 AK-3, AK-6): jeder Client bekommt dasselbe Gebiet, <c>Game:MapPath</c> wählt
/// die Datei, eine ungültige Datei verhindert den Start.
/// </summary>
public sealed class MapServerTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Der Start kann die Ausnahme verpackt melden (Einstiegspunkt, AggregateException); gesucht wird in der ganzen Kette.
    private static IEnumerable<Exception> Causes(Exception error)
    {
        for (var e = error; e is not null; e = e.InnerException)
        {
            yield return e;
            if (e is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions.SelectMany(Causes))
                {
                    yield return inner;
                }
            }
        }
    }

    [Fact]
    public async Task Two_players_get_the_same_area()
    {
        var (socketA, _, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var a = socketA;
        var (socketB, _, _) = await GameSockets.ConnectNewPlayerAsync(factory, Ct);
        using var b = socketB;

        var welcomeA = await GameSockets.ReceiveWelcomeAsync(a, Ct);
        var welcomeB = await GameSockets.ReceiveWelcomeAsync(b, Ct);

        Assert.NotNull(welcomeA.Area);
        Assert.Equal(welcomeA.Area, welcomeB.Area);
        Assert.Equal(GameLoop.AreaMessage(MapLoader.Load(TestMaps.MeadowPath)), welcomeA.Area);
    }

    [Fact]
    public async Task Map_path_setting_changes_the_area_in_welcome()
    {
        using var file = new TempMapFile(TestMaps.Small);
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting(GameSettings.MapPathKey, file.Path));
        var game = app.Services.GetRequiredService<GameLoop>();
        var connection = game.Join("map-path-test", "Map", null);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        var message = ServerMessage.Parser.ParseFrom(await connection.Outbox.Reader.ReadAsync(timeout.Token));
        game.Leave(connection);

        Assert.Equal(ServerMessage.PayloadOneofCase.Welcome, message.PayloadCase);
        var area = message.Welcome.Area;
        Assert.Equal(4u, area.Width);
        Assert.Equal(3u, area.Height);
        Assert.Equal(-1, area.OriginTx);
        Assert.Equal(-2, area.OriginTy);
        Assert.Equal(TestMaps.SmallGround, area.Ground.ToByteArray());
    }

    [Fact]
    public async Task Invalid_map_keeps_the_server_from_starting()
    {
        using var file = new TempMapFile(TestMaps.Invalid);
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting(GameSettings.MapPathKey, file.Path));

        var error = Record.Exception(() => app.Server);

        Assert.NotNull(error);
        Assert.Contains(Causes(error), e => e is MapFormatException { Location: TestMaps.InvalidLocation });
    }

    [Fact]
    public async Task Missing_map_keeps_the_server_from_starting()
    {
        using var file = new TempMapFile(TestMaps.Small);
        await using var app = factory.WithWebHostBuilder(b => b.UseSetting(GameSettings.MapPathKey, file.MissingPath));

        var error = Record.Exception(() => app.Server);

        Assert.NotNull(error);
        Assert.Contains(Causes(error), e => e is FileNotFoundException);
    }
}
