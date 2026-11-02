using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;

namespace BrowserGame.Admin.Tests;

/// <summary>
/// 005 AK-10–12: Farbschema. Hier Einbindung von theme.js und der Umschalter; das Verhalten im Browser
/// (System folgen, Wechsel, Neuladen, Abmelden, kein Aufblitzen) prüft T11 manuell.
/// </summary>
public sealed partial class AdminThemeTests
{
    private static readonly string[] SignedInPages = ["/", "/Smtp", "/Admins", "/Audit", "/Account"];

    private static string Text(string key) => AdminTexts.ResourceManager.GetString(key, CultureInfo.InvariantCulture)!;

    private static Task<AdminTestFlows.CreatedAdmin> SignedInAsync(AdminFactory factory) =>
        AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

    private static string Head(string html)
    {
        var start = html.IndexOf("<head>", StringComparison.Ordinal);
        var end = html.IndexOf("</head>", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return html[start..end];
    }

    /// <summary>HTML vom Start-Tag mit <paramref name="testId"/> bis zum passenden Ende-Tag (nicht verschachtelt).</summary>
    private static string Element(string html, string testId, string tag)
    {
        var marker = html.IndexOf($"data-testid=\"{testId}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"{testId} fehlt");
        var start = html.LastIndexOf($"<{tag}", marker, StringComparison.Ordinal);
        var end = html.IndexOf($"</{tag}>", marker, StringComparison.Ordinal);
        return html[start..(end + tag.Length + 3)];
    }

    private static void AssertThemeScriptBeforeCss(string html, string url)
    {
        var head = Head(html);
        var script = Assert.Single(ScriptTag().Matches(head), m => m.Value.Contains("src=\"/js/theme.js", StringComparison.Ordinal));
        Assert.False(Deferred().IsMatch(script.Value), $"{url}: {script.Value}");
        var firstCss = head.IndexOf("rel=\"stylesheet\"", StringComparison.Ordinal);
        Assert.True(firstCss > script.Index, url);
    }

    [Fact] // 005 AK-10, AK-11, AK-12
    public async Task Theme_script_runs_synchronously_in_the_head_before_the_css()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        var invite = await AdminTestFlows.InviteTokenAsync(factory);
        using var anonymous = factory.CreateFormClient();

        foreach (var url in SignedInPages)
        {
            var (response, html) = await admin.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertThemeScriptBeforeCss(html, url);
        }

        foreach (var url in new[] { "/Login", $"/Invite?token={invite}" })
        {
            var (response, html) = await anonymous.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            AssertThemeScriptBeforeCss(html, url);
        }
    }

    [Fact] // 005 AK-10, AK-11, AK-12
    public async Task Theme_script_is_served()
    {
        await using var factory = new AdminFactory();
        using var anonymous = factory.CreateFormClient();

        var (response, script) = await anonymous.GetAsync("/js/theme.js");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        foreach (var part in new[] { "\"admin-theme\"", "data-bs-theme", "(prefers-color-scheme: dark)", "data-theme-value", "DOMContentLoaded" })
        {
            Assert.Contains(part, script, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("export ", script, StringComparison.Ordinal);
        Assert.DoesNotContain("import ", script, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-11
    public async Task Sidebar_offers_light_dark_and_system_in_a_dropdown()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        foreach (var url in SignedInPages)
        {
            var (_, html) = await admin.Client.GetAsync(url);
            var themeSwitch = Element(html, "theme-switch", "div");

            Assert.Matches(DropdownToggle(), themeSwitch);
            Assert.Equal(["light", "dark", "auto"], ThemeValue().Matches(themeSwitch).Select(m => m.Groups[1].Value));
            Assert.All(ThemeItem().Matches(themeSwitch), m => Assert.Contains("type=\"button\"", m.Value, StringComparison.Ordinal));

            var text = WebUtility.HtmlDecode(themeSwitch);
            foreach (var key in new[] { "Theme.Label", "Theme.Light", "Theme.Dark", "Theme.System" })
            {
                Assert.Contains(Text(key), text, StringComparison.Ordinal);
            }

            var sidebar = html.IndexOf("id=\"admin-sidebar\"", StringComparison.Ordinal);
            var account = html.IndexOf("data-testid=\"sidebar-account\"", StringComparison.Ordinal);
            var theme = html.IndexOf("data-testid=\"theme-switch\"", StringComparison.Ordinal);
            var logout = html.IndexOf("data-testid=\"sidebar-logout\"", StringComparison.Ordinal);
            var main = html.IndexOf("<main", StringComparison.Ordinal);
            Assert.True(sidebar >= 0 && sidebar < account && account < theme && theme < logout && logout < main, url);
        }
    }

    [Fact] // 005 AK-4, AK-12
    public async Task Pages_without_sign_in_have_no_theme_switch()
    {
        await using var factory = new AdminFactory();
        using var anonymous = factory.CreateFormClient();

        var (_, html) = await anonymous.GetAsync("/Login");

        Assert.DoesNotContain("data-testid=\"theme-switch\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain("data-theme-value", html, StringComparison.Ordinal);
    }

    [GeneratedRegex("<script\\b[^>]*>")]
    private static partial Regex ScriptTag();

    [GeneratedRegex("\\b(defer|async)\\b|type=\"module\"")]
    private static partial Regex Deferred();

    [GeneratedRegex("data-theme-value=\"([^\"]*)\"")]
    private static partial Regex ThemeValue();

    [GeneratedRegex("<button\\b(?=[^>]*type=\"button\")(?=[^>]*data-bs-toggle=\"dropdown\")(?=[^>]*aria-expanded=\"false\")[^>]*>")]
    private static partial Regex DropdownToggle();

    [GeneratedRegex("<button\\b[^>]*data-theme-value=[^>]*>")]
    private static partial Regex ThemeItem();
}
