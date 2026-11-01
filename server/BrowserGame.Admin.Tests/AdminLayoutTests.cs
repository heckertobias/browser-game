using System.Net;

namespace BrowserGame.Admin.Tests;

public sealed class AdminLayoutTests
{
    private const string BootstrapCss = """href="/lib/bootstrap/bootstrap\.min\.css(\?v=[^"]+)?["]""";

    private static void AssertBootstrapOnly(HttpResponseMessage response, string html, string url)
    {
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{url}: {response.StatusCode}");
        Assert.Matches(BootstrapCss, html);
        Assert.DoesNotContain("/css/site.css", html, StringComparison.Ordinal);
    }

    [Fact] // 005 AK-5
    public async Task Anonymous_pages_use_bootstrap_not_tailwind()
    {
        await using var factory = new AdminFactory();
        var token = AdminTestFlows.SetupTokenFromLog(factory);
        using var client = factory.CreateFormClient();

        foreach (var url in new[] { "/Login", $"/Invite?token={token}" })
        {
            var (response, html) = await client.GetAsync(url);
            AssertBootstrapOnly(response, html, url);
        }
    }

    [Fact] // 005 AK-5
    public async Task Signed_in_pages_use_bootstrap_not_tailwind()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        foreach (var url in new[] { "/", "/Smtp", "/Admins", "/Audit", "/Account" })
        {
            var (response, html) = await admin.Client.GetAsync(url);
            AssertBootstrapOnly(response, html, url);
        }
    }
}
