using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class PlayerColorsTests
{
    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000001", 0x4363D8u)]
    [InlineData("3f2504e0-4f89-11d3-9a0c-0305e82c3301", 0x008080u)]
    public void Color_is_stable_for_account_id(string accountId, uint expected) =>
        Assert.Equal(expected, PlayerColors.For(accountId));

    [Fact]
    public void Different_accounts_get_varied_colors() =>
        Assert.True(Enumerable.Range(0, 50).Select(_ => PlayerColors.For(Guid.NewGuid().ToString())).Distinct().Count() > 1);
}
