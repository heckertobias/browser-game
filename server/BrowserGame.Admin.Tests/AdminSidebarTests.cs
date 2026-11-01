using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;
using BrowserGame.Server.Tests;

namespace BrowserGame.Admin.Tests;

public sealed partial class AdminSidebarTests
{
    private static readonly string[] Sections = ["/", "/Smtp", "/Admins", "/Audit"];
    private static readonly string[] SignedInPages = ["/", "/Smtp", "/Admins", "/Audit", "/Account"];

    private static string Text(string key) => AdminTexts.ResourceManager.GetString(key, CultureInfo.InvariantCulture)!;

    private static Task<AdminTestFlows.CreatedAdmin> SignedInAsync(AdminFactory factory) =>
        AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

    /// <summary>HTML vom Start-Tag mit <paramref name="testId"/> bis zum passenden Ende-Tag (nicht verschachtelt).</summary>
    private static string Element(string html, string testId, string tag)
    {
        var marker = html.IndexOf($"data-testid=\"{testId}\"", StringComparison.Ordinal);
        Assert.True(marker >= 0, $"{testId} fehlt");
        var start = html.LastIndexOf($"<{tag}", marker, StringComparison.Ordinal);
        var end = html.IndexOf($"</{tag}>", marker, StringComparison.Ordinal);
        return html[start..(end + tag.Length + 3)];
    }

    private static List<(string Href, bool Current, bool Active)> Links(string fragment) =>
        AnchorTag().Matches(fragment)
            .Select(m => (
                Href().Match(m.Value).Groups[1].Value,
                m.Value.Contains("aria-current=\"page\"", StringComparison.Ordinal),
                CssClass().Match(m.Value).Groups[1].Value.Split(' ').Contains("active")))
            .ToList();

    [Fact] // 005 AK-1
    public async Task Sidebar_lists_all_sections_and_marks_only_the_current_one()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        foreach (var url in Sections)
        {
            var (response, html) = await admin.Client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var links = Links(Element(html, "sidebar-nav", "nav"));

            Assert.Equal(Sections, links.Select(l => l.Href));
            Assert.Equal([url], links.Where(l => l.Current).Select(l => l.Href));
            Assert.Equal([url], links.Where(l => l.Active).Select(l => l.Href));
        }

        var (_, account) = await admin.Client.GetAsync("/Account");
        Assert.DoesNotContain(Links(Element(account, "sidebar-nav", "nav")), l => l.Current);
        Assert.Contains("aria-current=\"page\"", Element(account, "sidebar-account", "a"), StringComparison.Ordinal);
    }

    [Fact] // 005 AK-2
    public async Task Every_signed_in_page_offers_account_and_sign_out()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        foreach (var url in SignedInPages)
        {
            var (_, html) = await admin.Client.GetAsync(url);
            var account = Element(html, "sidebar-account", "a");
            var logout = Element(html, "sidebar-logout", "form");

            Assert.Contains("href=\"/Account\"", account, StringComparison.Ordinal);
            Assert.Contains("method=\"post\"", logout, StringComparison.Ordinal);
            Assert.Contains("action=\"/Logout\"", logout, StringComparison.Ordinal);
            Assert.Contains("name=\"__RequestVerificationToken\"", logout, StringComparison.Ordinal);
            var sidebar = html.IndexOf("id=\"admin-sidebar\"", StringComparison.Ordinal);
            var accountAt = html.IndexOf("data-testid=\"sidebar-account\"", StringComparison.Ordinal);
            var main = html.IndexOf("<main", StringComparison.Ordinal);
            Assert.True(sidebar >= 0 && sidebar < accountAt && accountAt < main, url);
        }
    }

    [Fact] // 005 AK-2
    public async Task Sign_out_from_the_sidebar_ends_the_session()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        var (_, page) = await admin.Client.GetAsync("/Smtp");
        var token = FormClient.AntiforgeryToken(Element(page, "sidebar-logout", "form"));

        var (logout, _) = await admin.Client.PostWithTokenAsync("/Logout", new Dictionary<string, string>(), token);
        var (after, _) = await admin.Client.GetAsync("/Smtp");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/Login", logout.Headers.Location?.OriginalString);
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.Equal("/Login", after.Headers.Location?.AbsolutePath);
    }

    [Fact] // 005 AK-3 (Struktur; Optik manuell)
    public async Task Narrow_screens_get_a_menu_button_for_the_offcanvas_sidebar()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.GetAsync("/");

        var marker = html.IndexOf("id=\"admin-sidebar\"", StringComparison.Ordinal);
        Assert.True(marker >= 0);
        var startTag = html[html.LastIndexOf("<div", marker, StringComparison.Ordinal)..marker];
        Assert.Contains("offcanvas-lg", startTag, StringComparison.Ordinal);
        Assert.Matches(MenuButton(), html);
        Assert.Contains("src=\"/lib/bootstrap/bootstrap.bundle.min.js", html, StringComparison.Ordinal);
        Assert.Contains("name=\"viewport\" content=\"width=device-width", html, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-5
    public async Task Admin_css_and_icons_are_linked_and_admin_css_is_served()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        var (_, html) = await admin.Client.GetAsync("/");
        var bootstrap = html.IndexOf("href=\"/lib/bootstrap/bootstrap.min.css", StringComparison.Ordinal);
        var adminCss = html.IndexOf("href=\"/css/admin.css", StringComparison.Ordinal);
        var (css, _) = await admin.Client.GetAsync("/css/admin.css");

        Assert.Contains("href=\"/lib/bootstrap-icons/bootstrap-icons.min.css", html, StringComparison.Ordinal);
        Assert.True(bootstrap >= 0 && bootstrap < adminCss);
        Assert.Equal(HttpStatusCode.OK, css.StatusCode);
        Assert.Equal("text/css", css.Content.Headers.ContentType?.MediaType);
    }

    [Fact] // 005 AK-5 (CSP unverändert)
    public async Task Pages_have_no_inline_scripts_or_styles()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        using var anonymous = factory.CreateFormClient();
        var pages = new List<string>();
        foreach (var url in SignedInPages)
        {
            pages.Add((await admin.Client.GetAsync(url)).Html);
        }

        pages.Add((await anonymous.GetAsync("/Login")).Html);

        foreach (var html in pages)
        {
            Assert.DoesNotMatch(InlineScript(), html);
            Assert.DoesNotContain(" style=\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("<style", html, StringComparison.Ordinal);
        }
    }

    [Theory] // 005 AK-14
    [MemberData(nameof(AdminLanguageTests.Requests), MemberType = typeof(AdminLanguageTests))]
    public async Task Html_lang_is_english_regardless_of_browser_language(string? acceptLanguage, string? cookie)
    {
        await using var factory = new AdminFactory();
        using var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        if (acceptLanguage is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", acceptLanguage);
        }

        if (cookie is not null)
        {
            client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        var (_, html) = await client.GetAsync("/Login");

        Assert.Contains("<html lang=\"en\"", html, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-14
    public async Task Signed_in_layout_is_english()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        admin.Client.Http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "de-DE,de;q=0.9");

        var (_, html) = await admin.Client.GetAsync("/");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("<html lang=\"en\"", html, StringComparison.Ordinal);
        foreach (var key in new[] { "Nav.Overview", "Nav.Smtp", "Nav.Admins", "Nav.Audit", "Layout.SignOut" })
        {
            Assert.Contains(Text(key), text, StringComparison.Ordinal);
        }

        Assert.Contains($"aria-label=\"{Text("Layout.Navigation")}\"", text, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-4
    public async Task Anonymous_pages_have_no_sidebar()
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        using var client = factory.CreateFormClient();

        foreach (var url in new[] { "/Login", $"/Invite?token={token}" })
        {
            var (response, html) = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.DoesNotContain("id=\"admin-sidebar\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-testid=\"sidebar-nav\"", html, StringComparison.Ordinal);
            Assert.DoesNotContain("data-bs-toggle=\"offcanvas\"", html, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex("<a\\b[^>]*>")]
    private static partial Regex AnchorTag();

    [GeneratedRegex("href=\"([^\"]*)\"")]
    private static partial Regex Href();

    [GeneratedRegex("class=\"([^\"]*)\"")]
    private static partial Regex CssClass();

    [GeneratedRegex("<button\\b(?=[^>]*data-bs-toggle=\"offcanvas\")(?=[^>]*data-bs-target=\"#admin-sidebar\")(?=[^>]*aria-controls=\"admin-sidebar\")[^>]*>")]
    private static partial Regex MenuButton();

    // Ausführbares Inline-Skript: <script> ohne src und nicht type="application/json" (Passkey-Optionen sind Daten).
    [GeneratedRegex("<script\\b(?![^>]*\\bsrc=)(?![^>]*type=\"application/json\")[^>]*>")]
    private static partial Regex InlineScript();
}
