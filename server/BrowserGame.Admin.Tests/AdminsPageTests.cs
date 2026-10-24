using System.Net;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Tests;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Admin.Tests;

public sealed partial class AdminsPageTests
{
    [GeneratedRegex("data-testid=\"invite-link\"|value=\"([^\"]+/Invite\\?token=[^\"]+)\"")]
    private static partial Regex InviteLinkPattern();

    private static string InviteTokenFrom(string html)
    {
        var link = InviteLinkPattern().Matches(html).Select(m => m.Groups[1].Value).First(v => v.Length > 0);
        return WebUtility.HtmlDecode(link).Split("token=")[1];
    }

    [Fact] // AK-4e, AK-12
    public async Task Invited_admin_can_join_and_it_is_audited()
    {
        await using var factory = new AdminFactory();
        var first = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await first.Client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "" });
        var second = await AdminTestFlows.RedeemWithPasswordAsync(factory, InviteTokenFrom(html), "Zweiter", "zweiter@example.org");

        var (_, list) = await first.Client.GetAsync("/Admins");
        Assert.Contains("Zweiter", list, StringComparison.Ordinal);
        var (_, audit) = await second.Client.GetAsync("/Audit");
        Assert.Contains("Admin eingeladen", audit, StringComparison.Ordinal);
        Assert.Contains("Einladung eingel", audit, StringComparison.Ordinal);
        Assert.Contains("eingeladen von Tobias", audit, StringComparison.Ordinal);
    }

    [Fact] // AK-4e: mit E-Mail und eingerichtetem SMTP geht der Link auch per Mail raus
    public async Task Invite_with_email_is_mailed_when_smtp_is_configured()
    {
        await using var factory = new AdminFactory();
        await using var smtp = new FakeSmtpServer();
        var first = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().SaveAsync(
                new SmtpSettings("127.0.0.1", smtp.Port, SmtpSecurity.None, smtp.Username, smtp.Password, "noreply@example.net", null),
                TestContext.Current.CancellationToken);
        }

        var (_, html) = await first.Client.PostAsync("/Admins", "/Admins?handler=Invite", new Dictionary<string, string> { ["InviteEmail"] = "neu@example.org" });

        Assert.Contains("Mail an neu@example.org verschickt", html, StringComparison.Ordinal);
        Assert.Contains("/Invite?token=", Assert.Single(smtp.Messages), StringComparison.Ordinal);
    }

    [Fact] // AK-4f
    public async Task Admin_can_remove_another_admin_but_not_themselves()
    {
        await using var factory = new AdminFactory();
        var first = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        var second = await AdminTestFlows.RedeemWithPasswordAsync(factory, await AdminTestFlows.InviteTokenAsync(factory), "Zweiter", "zweiter@example.org");

        string firstId, secondId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>();
            firstId = (await users.FindByEmailAsync("admin@example.org"))!.Id;
            secondId = (await users.FindByEmailAsync("zweiter@example.org"))!.Id;
        }

        var (_, self) = await first.Client.PostAsync("/Admins", "/Admins?handler=Remove", new Dictionary<string, string> { ["RemoveId"] = firstId });
        Assert.Contains("nicht selbst entfernen", self, StringComparison.Ordinal);

        var (_, removed) = await first.Client.PostAsync("/Admins", "/Admins?handler=Remove", new Dictionary<string, string> { ["RemoveId"] = secondId });
        Assert.Contains("Zweiter wurde entfernt", removed, StringComparison.Ordinal);

        using var client = factory.CreateFormClient();
        var (login, _) = await AdminTestFlows.LoginPasswordAsync(client, second.Email);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode); // entfernter Admin kommt nicht mehr rein
    }

    [Fact] // AK-4f: der einzige Admin bleibt
    public async Task Only_admin_cannot_be_removed()
    {
        await using var factory = new AdminFactory();
        var only = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");
        string id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            id = (await scope.ServiceProvider.GetRequiredService<UserManager<AdminUser>>().FindByEmailAsync("admin@example.org"))!.Id;
        }

        await only.Client.PostAsync("/Admins", "/Admins?handler=Remove", new Dictionary<string, string> { ["RemoveId"] = id });

        await using var check = factory.Services.CreateAsyncScope();
        Assert.NotNull(await check.ServiceProvider.GetRequiredService<UserManager<AdminUser>>().FindByIdAsync(id));
    }
}
