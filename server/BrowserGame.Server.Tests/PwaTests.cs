using System.Buffers.Binary;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

public sealed partial class PwaTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static ReadOnlySpan<byte> PngSignature => [137, 80, 78, 71, 13, 10, 26, 10];

    private static (int Width, int Height, byte ColorType) ReadPngHeader(byte[] png)
    {
        Assert.True(png.AsSpan(0, 8).SequenceEqual(PngSignature));
        Assert.True(png.AsSpan(12, 4).SequenceEqual("IHDR"u8));
        return ((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)),
                (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)), png[25]);
    }

    private async Task<byte[]> GetIconAsync(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        return await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
    }

    private async Task<JsonDocument> GetManifestAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/manifest.webmanifest", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/manifest+json", response.Content.Headers.ContentType?.MediaType);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact] // AK-10, AK-11
    public async Task Manifest_is_served_with_install_fields()
    {
        using var manifest = await GetManifestAsync();
        var root = manifest.RootElement;

        Assert.Equal("/play", root.GetProperty("id").GetString());
        Assert.Equal("/play", root.GetProperty("start_url").GetString());
        Assert.Equal("/", root.GetProperty("scope").GetString());
        Assert.Equal("standalone", root.GetProperty("display").GetString());
        Assert.Equal("Browser Game", root.GetProperty("name").GetString());
        Assert.Equal("Browser Game", root.GetProperty("short_name").GetString());
        Assert.Equal("#000000", root.GetProperty("theme_color").GetString());
        Assert.Equal("#000000", root.GetProperty("background_color").GetString());

        var icons = root.GetProperty("icons").EnumerateArray().ToList();
        Assert.Equal(3, icons.Count);
        Assert.Single(icons, i => i.GetProperty("sizes").GetString() == "192x192" && !i.TryGetProperty("purpose", out _));
        Assert.Single(icons, i => i.GetProperty("sizes").GetString() == "512x512" && !i.TryGetProperty("purpose", out _));
        Assert.Single(icons, i => i.TryGetProperty("purpose", out var p) && p.GetString() == "maskable");
    }

    [Fact] // AK-10
    public async Task Manifest_icons_exist_with_declared_size()
    {
        using var manifest = await GetManifestAsync();

        foreach (var icon in manifest.RootElement.GetProperty("icons").EnumerateArray())
        {
            var png = await GetIconAsync(icon.GetProperty("src").GetString()!);
            var (width, height, colorType) = ReadPngHeader(png);

            Assert.Equal("image/png", icon.GetProperty("type").GetString());
            Assert.Equal($"{width}x{height}", icon.GetProperty("sizes").GetString());
            Assert.Equal(2, colorType);
        }
    }

    [Fact] // AK-10
    public async Task Apple_touch_icon_is_180_and_opaque()
    {
        var png = await GetIconAsync("/icons/apple-touch-icon.png");
        var (width, height, colorType) = ReadPngHeader(png);

        Assert.Equal(180, width);
        Assert.Equal(180, height);
        Assert.Equal(2, colorType);
        Assert.False(png.AsSpan().IndexOf("tRNS"u8) >= 0);
    }

    [Theory] // AK-10
    [InlineData("/")]
    [InlineData("/Account/Login")]
    public async Task Pages_link_manifest_and_icons(string path)
    {
        var html = await factory.CreateClient().GetStringAsync(new Uri(path, UriKind.Relative), TestContext.Current.CancellationToken);

        string[] tags =
        [
            "rel=\"manifest\" href=\"/manifest.webmanifest\"",
            "name=\"theme-color\" content=\"#000000\"",
            "rel=\"icon\" type=\"image/png\" href=\"/icons/icon-192.png\"",
            "rel=\"apple-touch-icon\" href=\"/icons/apple-touch-icon.png\"",
            "name=\"apple-mobile-web-app-title\" content=\"Browser Game\"",
            "name=\"mobile-web-app-capable\" content=\"yes\"",
            "name=\"apple-mobile-web-app-capable\" content=\"yes\"",
            "name=\"apple-mobile-web-app-status-bar-style\" content=\"black\"",
        ];
        foreach (var tag in tags)
            Assert.Contains(tag, html, StringComparison.Ordinal);
    }

    [GeneratedRegex("/play/assets/[A-Za-z0-9._-]+")]
    private static partial Regex AssetPath();

    [Fact] // AK-12, AK-13
    public async Task Service_worker_is_served_anonymously_without_caching()
    {
        using var client = factory.CreateFormClient();
        var (offline, offlineHtml) = await client.GetAsync("/play/offline.html");
        var assets = AssetPath().Matches(offlineHtml).Select(m => m.Value).Distinct().ToList();
        Assert.NotEmpty(assets);

        var (response, script) = await client.GetAsync("/sw.js");

        Assert.Equal(HttpStatusCode.OK, offline.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("utf-8", response.Content.Headers.ContentType?.CharSet);
        Assert.True(response.Headers.CacheControl?.NoCache);
        Assert.Contains("\"/play/offline.html\"", script, StringComparison.Ordinal);
        foreach (var asset in assets)
            Assert.Contains($"\"{asset}\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("__PRECACHE__", script, StringComparison.Ordinal);
    }

    [Fact] // AK-12, AK-13
    public async Task Offline_page_is_served_anonymously_without_caching()
    {
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/play/offline.html");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Theory] // AK-13
    [InlineData("/play")]
    [InlineData("/play/")]
    [InlineData("/play/irgendwas")]
    public async Task Game_page_is_not_cached(string path)
    {
        using var client = factory.CreateFormClient();
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        await AccountTestFlows.LoginAsync(client, account.Email);

        var (response, html) = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<div id=\"root\">", html, StringComparison.Ordinal);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact] // AK-13
    public async Task Play_assets_are_immutable()
    {
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/play/assets/test-asset.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cache = response.Headers.CacheControl!;
        Assert.True(cache.Public);
        Assert.Equal(TimeSpan.FromDays(365), cache.MaxAge);
        Assert.Contains(cache.Extensions, e => e.Name == "immutable");
        Assert.False(cache.NoCache);
    }

    [Fact] // AK-13
    public async Task Manifest_is_not_cached()
    {
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/manifest.webmanifest");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoCache);
    }

    [Fact]
    public async Task Other_static_files_keep_default_caching()
    {
        using var client = factory.CreateFormClient();

        var (response, _) = await client.GetAsync("/icons/icon-192.png");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.CacheControl);
    }
}
