using System.Net;
using System.Text.RegularExpressions;

namespace BrowserGame.Admin.Tests;

/// <summary>005 AK-4: Seiten ohne Anmeldung nutzen _LayoutPlain – Karte mit Formular, kein Menü.</summary>
public sealed partial class AdminPlainLayoutTests
{
    private static Task<AdminTestFlows.CreatedAdmin> SignedInAsync(AdminFactory factory) =>
        AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

    /// <summary>Login, Einladung (Schritt Details) und zweiter Faktor, abgerufen von einem nicht angemeldeten Client.</summary>
    private static async Task<List<(string Url, string Html)>> PlainPagesAsync(AdminFactory factory, AdminTestFlows.CreatedAdmin admin)
    {
        var invite = await AdminTestFlows.InviteTokenAsync(factory);
        using var client = factory.CreateFormClient();
        var pages = new List<(string Url, string Html)>();
        foreach (var url in new[] { "/Login", $"/Invite?token={invite}" })
        {
            var (response, html) = await client.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            pages.Add((url, html));
        }

        var (password, _) = await AdminTestFlows.LoginPasswordAsync(client, admin.Email);
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        var (twoFactor, twoFactorHtml) = await client.GetAsync("/LoginTwoFactor");
        Assert.Equal(HttpStatusCode.OK, twoFactor.StatusCode);
        pages.Add(("/LoginTwoFactor", twoFactorHtml));
        return pages;
    }

    private static string Head(string html)
    {
        var start = html.IndexOf("<head>", StringComparison.Ordinal);
        var end = html.IndexOf("</head>", StringComparison.Ordinal);
        Assert.True(start >= 0 && end > start);
        return Title().Replace(html[start..end], "");
    }

    private static void AssertPlainLayout(string html, string url)
    {
        var main = html.IndexOf("<main", StringComparison.Ordinal);
        Assert.True(main >= 0 && main == html.LastIndexOf("<main", StringComparison.Ordinal), url);
        var startTag = html[main..(html.IndexOf('>', main) + 1)];
        Assert.True(startTag.Contains("data-testid=\"plain-layout\"", StringComparison.Ordinal), url);
        Assert.True(startTag.Contains("align-items-center", StringComparison.Ordinal), url);
        Assert.True(startTag.Contains("justify-content-center", StringComparison.Ordinal), url);

        var card = html.IndexOf("data-testid=\"plain-card\"", StringComparison.Ordinal);
        var title = html.IndexOf("<h1", StringComparison.Ordinal);
        var form = html.IndexOf("<form", StringComparison.Ordinal);
        var end = html.IndexOf("</main>", StringComparison.Ordinal);
        Assert.True(card >= 0 && card < title && title < form && form < end, url);

        foreach (var forbidden in new[]
        {
            "id=\"admin-sidebar\"", "data-testid=\"sidebar-nav\"", "data-testid=\"sidebar-account\"",
            "data-testid=\"sidebar-logout\"", "data-bs-toggle=\"offcanvas\"", "<nav",
        })
        {
            Assert.False(html.Contains(forbidden, StringComparison.Ordinal), $"{url}: {forbidden}");
        }
    }

    [Fact] // 005 AK-4
    public async Task Pages_without_sign_in_show_only_the_form_in_a_centered_card()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        foreach (var (url, html) in await PlainPagesAsync(factory, admin))
        {
            AssertPlainLayout(html, url);
        }
    }

    [Fact] // 005 AK-4
    public async Task Plain_layout_does_not_depend_on_sign_in()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        var (response, html) = await admin.Client.GetAsync("/Login");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPlainLayout(html, "/Login");
    }

    [Fact] // 005 AK-4, AK-5, AK-14
    public async Task Plain_pages_share_the_head_of_the_signed_in_layout()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);
        var pages = await PlainPagesAsync(factory, admin);
        var (response, signedIn) = await admin.Client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        foreach (var (url, html) in pages)
        {
            Assert.True(Head(signedIn) == Head(html), url);
            Assert.True(html.Contains("<html lang=\"en\"", StringComparison.Ordinal), url);
        }
    }

    [Fact] // 005 AK-4, AK-13
    public async Task Passkey_script_is_still_loaded_after_the_passkey_forms()
    {
        await using var factory = new AdminFactory();
        await SignedInAsync(factory);
        var token = await AdminTestFlows.InviteTokenAsync(factory);
        using var client = factory.CreateFormClient();

        var (_, login) = await client.GetAsync("/Login");
        var (_, chooseMethod) = await client.PostAsync($"/Invite?token={token}", "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = "Zweiter", ["Email"] = "second@example.org" });

        AssertScriptAfter(login, "data-passkey=\"get\"");
        AssertScriptAfter(chooseMethod, "data-passkey=\"create\"");
    }

    private static void AssertScriptAfter(string html, string form)
    {
        var formAt = html.IndexOf(form, StringComparison.Ordinal);
        var script = html.IndexOf("<script type=\"module\" src=\"/js/passkeys.js", StringComparison.Ordinal);
        var body = html.IndexOf("</body>", StringComparison.Ordinal);
        Assert.True(formAt >= 0 && formAt < script && script < body, form);
    }

    [Fact] // 005 AK-5
    public async Task Plain_pages_have_no_inline_scripts_or_styles()
    {
        await using var factory = new AdminFactory();
        var admin = await SignedInAsync(factory);

        foreach (var (url, html) in await PlainPagesAsync(factory, admin))
        {
            Assert.False(InlineScript().IsMatch(html), url);
            Assert.False(html.Contains(" style=\"", StringComparison.Ordinal), url);
            Assert.False(html.Contains("<style", StringComparison.Ordinal), url);
        }
    }

    [GeneratedRegex("<title>[^<]*</title>")]
    private static partial Regex Title();

    [GeneratedRegex("<script\\b(?![^>]*\\bsrc=)(?![^>]*type=\"application/json\")[^>]*>")]
    private static partial Regex InlineScript();
}
