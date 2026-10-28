using System.Net;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

public sealed class AccountDeleteTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static Task<(HttpResponseMessage Response, string Html)> DeleteAsync(FormClient client, string password) =>
        client.PostAsync("/Account/Delete", "/Account/Delete", new Dictionary<string, string> { ["Password"] = password });

    private async Task<Account?> FindAsync(string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
    }

    [Fact] // AK-16
    public async Task Wrong_or_missing_password_keeps_account()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;

        var (_, wrong) = await DeleteAsync(client, "falsches-passwort");
        var (_, missing) = await DeleteAsync(client, "");

        Assert.Contains("Falsches Passwort", HtmlTexts.Visible(wrong), StringComparison.Ordinal);
        Assert.Contains("Bitte Passwort angeben.", HtmlTexts.Visible(missing), StringComparison.Ordinal);
        var user = await FindAsync(account.Email);
        Assert.NotNull(user);
        Assert.Equal(1, user.AccessFailedCount);
    }

    [Fact] // AK-16
    public async Task Delete_with_password_removes_account_and_data_and_frees_name()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);
        var id = (await FindAsync(account.Email))!.Id;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            db.PlayerPositions.Add(new PlayerPosition { AccountId = id, X = 1, Y = 2 });
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var (response, html) = await DeleteAsync(client, AccountTestFlows.Password);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Dein Account wurde gelöscht", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith("bg.auth=;", StringComparison.Ordinal));
        Assert.Null(await FindAsync(account.Email));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
            Assert.False(await db.PlayerPositions.AnyAsync(p => p.AccountId == id, TestContext.Current.CancellationToken));
            Assert.False(await db.Set<IdentityUserPasskey<string>>().AnyAsync(p => p.UserId == id, TestContext.Current.CancellationToken));
        }

        var (play, _) = await client.GetAsync("/play");
        Assert.Equal(HttpStatusCode.Redirect, play.StatusCode);
        Assert.Contains("/Account/Login", play.Headers.Location?.OriginalString, StringComparison.Ordinal);
        using var again = factory.CreateFormClient();
        var (_, registered) = await AccountTestFlows.RegisterAsync(again, account.Email, account.Name);
        Assert.Contains("Fast geschafft", HtmlTexts.Visible(registered), StringComparison.Ordinal);
        Assert.Equal(account.Name, (await FindAsync(account.Email))?.UserName);
    }

    [Fact] // AK-16
    public async Task Delete_ends_other_sessions()
    {
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero)));
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        using var deleting = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        using var other = new FormClient(app.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
        await AccountTestFlows.LoginAsync(deleting, account.Email);
        await AccountTestFlows.LoginAsync(other, account.Email);
        Assert.Equal(HttpStatusCode.OK, (await other.GetAsync("/play")).Response.StatusCode);

        await DeleteAsync(deleting, AccountTestFlows.Password);

        var (after, _) = await other.GetAsync("/play");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
    }
}
