using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BrowserGame.Server.Pwa;

namespace BrowserGame.Server.Tests;

public sealed partial class ServiceWorkerScriptTests
{
    private const string OfflineHtml =
        """
        <!doctype html>
        <script type="module" crossorigin src="/play/assets/offline-Ab1_c-2.js"></script>
        <link rel="modulepreload" href="/play/assets/offline-Ab1_c-2.js">
        <link rel="stylesheet" href="/play/assets/styles-XyZ.css">
        <link rel="icon" href="/icons/icon-192.png">
        """;

    private static readonly string[] ExpectedPrecache =
        ["/play/offline.html", "/play/assets/offline-Ab1_c-2.js", "/play/assets/styles-XyZ.css"];

    [GeneratedRegex("""const VERSION = "([^"]*)";""")]
    private static partial Regex VersionLine();

    [GeneratedRegex(@"const PRECACHE = (\[[^\]]*\]);")]
    private static partial Regex PrecacheLine();

    private static (string Version, string[] Precache) Parse(string script) =>
        (VersionLine().Match(script).Groups[1].Value,
         JsonSerializer.Deserialize<string[]>(PrecacheLine().Match(script).Groups[1].Value)!);

    [Fact] // AK-12
    public void Precache_lists_offline_page_and_its_assets_once()
    {
        var (_, precache) = Parse(ServiceWorkerScript.Render(OfflineHtml));

        Assert.Equal(ExpectedPrecache, precache);
    }

    [Fact] // AK-12, AK-16
    public void Precached_assets_are_served_from_cache_first()
    {
        var script = ServiceWorkerScript.Render(OfflineHtml);

        Assert.Contains("caches.match(url.pathname).then((r) => r ?? fetch(request))", script, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(request).catch(", script, StringComparison.Ordinal);
    }

    [Fact] // AK-13
    public void Version_is_sha256_prefix_of_html()
    {
        var (version, _) = Parse(ServiceWorkerScript.Render(OfflineHtml));

        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(OfflineHtml)))[..16], version);
        Assert.Matches("^[0-9a-f]{16}$", version);
    }

    [Fact] // AK-13
    public void Version_changes_when_html_changes()
    {
        var before = ServiceWorkerScript.Render(OfflineHtml);
        var after = ServiceWorkerScript.Render(OfflineHtml.Replace("Ab1_c-2", "Zz9", StringComparison.Ordinal));

        Assert.NotEqual(Parse(before).Version, Parse(after).Version);
        Assert.NotEqual(before, after);
    }

    [Fact] // AK-13
    public void Same_html_gives_same_script()
    {
        Assert.Equal(ServiceWorkerScript.Render(OfflineHtml), ServiceWorkerScript.Render(OfflineHtml));
    }

    [Fact] // AK-12
    public void Without_offline_page_precache_is_empty_and_version_none()
    {
        var (version, precache) = Parse(ServiceWorkerScript.Render(null));

        Assert.Equal("none", version);
        Assert.Empty(precache);
    }

    [Theory] // AK-12
    [InlineData(null)]
    [InlineData(OfflineHtml)]
    public void Output_contains_no_placeholders(string? html)
    {
        var script = ServiceWorkerScript.Render(html);

        Assert.DoesNotContain("__VERSION__", script, StringComparison.Ordinal);
        Assert.DoesNotContain("__PRECACHE__", script, StringComparison.Ordinal);
        Assert.Contains("const OFFLINE_PAGE = \"/play/offline.html\"", script, StringComparison.Ordinal);
    }
}
