using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Server.Tests;

public class PositionStoreTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<string> NewAccountIdAsync()
    {
        var account = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        return await GameSockets.AccountIdAsync(factory, account.Email);
    }

    private async Task<List<PlayerPosition>> RowsAsync(string accountId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        return await db.PlayerPositions.AsNoTracking().Where(p => p.AccountId == accountId).ToListAsync(Ct);
    }

    [Fact] // AK-18
    public async Task Save_inserts_then_updates_position()
    {
        var id = await NewAccountIdAsync();
        var store = factory.Services.GetRequiredService<PositionStore>();

        await store.SaveAsync([new PositionToSave(id, 1, 2)], Ct);
        var first = Assert.Single(await RowsAsync(id));
        Assert.Equal((1d, 2d), (first.X, first.Y));

        await store.SaveAsync([new PositionToSave(id, 3, 4)], Ct);
        var second = Assert.Single(await RowsAsync(id));
        Assert.Equal((3d, 4d), (second.X, second.Y));
        Assert.True(DateTimeOffset.UtcNow - second.UpdatedAt < TimeSpan.FromMinutes(1));
    }

    [Fact] // AK-16, AK-18
    public async Task Save_skips_deleted_accounts()
    {
        var kept = await NewAccountIdAsync();
        var gone = await NewAccountIdAsync();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
            Assert.True((await users.DeleteAsync((await users.FindByIdAsync(gone))!)).Succeeded);
        }

        var store = factory.Services.GetRequiredService<PositionStore>();
        await store.SaveAsync([new PositionToSave(gone, 5, 6), new PositionToSave(kept, 7, 8), new PositionToSave("nie-angelegt", 9, 9)], Ct);

        var row = Assert.Single(await RowsAsync(kept));
        Assert.Equal((7d, 8d), (row.X, row.Y));
        Assert.Empty(await RowsAsync(gone));
        Assert.Empty(await RowsAsync("nie-angelegt"));
    }
}
