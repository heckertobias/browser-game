using BrowserGame.Server.Accounts;

namespace BrowserGame.Server.Tests;

public sealed class AccountCredentialsTests
{
    [Theory] // AK-13c
    [InlineData(false, 0, false)]
    [InlineData(false, 1, false)]
    [InlineData(true, 0, false)]
    [InlineData(true, 1, true)]
    [InlineData(false, 2, true)]
    [InlineData(true, 2, true)]
    public void Credential_can_be_removed_only_if_another_remains(bool hasPassword, int passkeys, bool expected) =>
        Assert.Equal(expected, AccountCredentials.CanRemoveCredential(hasPassword, passkeys));
}
