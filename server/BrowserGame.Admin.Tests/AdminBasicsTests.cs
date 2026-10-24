using System.Net;
using BrowserGame.Data;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Tests;

public sealed class AdminBasicsTests(AdminFactory factory) : IClassFixture<AdminFactory>
{
    [Fact]
    public async Task Health_is_ok()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Responses_carry_security_headers()
    {
        var response = await factory.CreateClient().GetAsync(new Uri("/health", UriKind.Relative), TestContext.Current.CancellationToken);

        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("frame-ancestors 'none'", response.Headers.GetValues("Content-Security-Policy").Single(), StringComparison.Ordinal);
    }

    [Fact] // 004 AK-5: Admin richtet die Datenbank selbst ein, auch wenn das Spiel nie lief
    public async Task Admin_migrates_game_and_admin_schema_on_its_own()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var game = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var admin = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        Assert.Empty(await game.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await admin.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, await admin.Users.CountAsync(TestContext.Current.CancellationToken));
    }
}
