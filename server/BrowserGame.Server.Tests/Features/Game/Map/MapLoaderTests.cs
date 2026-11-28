using BrowserGame.Server.Features.Game;
using Google.Protobuf;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Karte beim Start laden (018 AK-3, AK-6): Standarddatei oder <c>Game:MapPath</c>, gleiche Bytes bei jedem Laden,
/// Fehler mit Fundstelle im Log.
/// </summary>
public sealed class MapLoaderTests
{
    private static IConfiguration Config(string? mapPath) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [GameSettings.MapPathKey] = mapPath })
            .Build();

    [Fact]
    public void Loading_twice_gives_the_same_bytes()
    {
        var a = GameLoop.AreaMessage(new MapLoader(Config(null)).Map).ToByteArray();
        var b = GameLoop.AreaMessage(new MapLoader(Config(null)).Map).ToByteArray();

        Assert.NotEmpty(a);
        Assert.Equal(a, b);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Maps/meadow.json")]
    public void Without_setting_the_meadow_next_to_the_application_is_loaded(string? value)
    {
        var expected = MapFile.Parse(File.ReadAllText(TestMaps.MeadowPath));

        var map = new MapLoader(Config(value)).Map;

        Assert.Equal(expected.Ground, map.Ground);
        Assert.Equal(expected.Width, map.Width);
        Assert.Equal(expected.Height, map.Height);
        Assert.Equal(expected.OriginTx, map.OriginTx);
        Assert.Equal(expected.OriginTy, map.OriginTy);
    }

    [Fact]
    public void Map_path_setting_names_the_file()
    {
        using var file = new TempMapFile(TestMaps.Small);

        var map = new MapLoader(Config(file.Path)).Map;

        Assert.Equal(4, map.Width);
        Assert.Equal(3, map.Height);
        Assert.Equal(16, map.TileSize);
        Assert.Equal(-1, map.OriginTx);
        Assert.Equal(-2, map.OriginTy);
    }

    [Fact]
    public void Invalid_file_fails_and_logs_location_and_reason()
    {
        using var file = new TempMapFile(TestMaps.Invalid);
        var logger = new ListLogger();

        var error = Assert.Throws<MapFormatException>(() => new MapLoader(Config(file.Path), logger));

        Assert.Equal(TestMaps.InvalidLocation, error.Location);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Contains(file.Path, entry.Message);
        Assert.Contains(error.Message, entry.Message);
    }

    [Fact]
    public void Missing_file_fails_and_logs_the_path()
    {
        using var file = new TempMapFile(TestMaps.Small);
        var logger = new ListLogger();

        Assert.Throws<FileNotFoundException>(() => new MapLoader(Config(file.MissingPath), logger));

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Critical, entry.Level);
        Assert.Contains(file.MissingPath, entry.Message);
    }

    private sealed class ListLogger : ILogger<MapLoader>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }
}
