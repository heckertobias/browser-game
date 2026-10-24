using BrowserGame.Admin.Auth;

namespace BrowserGame.Admin.Tests;

public sealed class AdminAccountTests
{
    [Theory] // AK-4b/AK-4f: nur Passkey oder Passwort + 2FA gilt als eingerichtete Anmeldung
    [InlineData(false, false, 0, false)]
    [InlineData(true, false, 0, false)] // Passwort ohne zweiten Faktor reicht nicht
    [InlineData(true, true, 0, true)]
    [InlineData(false, false, 1, true)]
    [InlineData(true, true, 2, true)]
    public void Active_requires_passkey_or_password_with_two_factor(bool hasPassword, bool twoFactor, int passkeys, bool expected)
    {
        Assert.Equal(expected, AdminCredentials.IsActive(hasPassword, twoFactor, passkeys));
    }

    [Fact] // AK-4c: neue Codes machen die alten ungültig
    public async Task New_recovery_codes_replace_old_ones()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.PostAsync("/Account", "/Account?handler=RecoveryCodes", new Dictionary<string, string>());
        Assert.Contains("die alten gelten ab sofort nicht mehr", html, StringComparison.Ordinal);

        using var client = factory.CreateFormClient();
        await AdminTestFlows.LoginPasswordAsync(client, admin.Email);
        var (response, _) = await AdminTestFlows.LoginCodeAsync(client, admin.RecoveryCodes[0], recovery: true);
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode); // alter Code abgelehnt
    }

    [Fact]
    public async Task Account_page_shows_password_status()
    {
        await using var factory = new AdminFactory();
        var admin = await AdminTestFlows.RedeemWithPasswordAsync(factory, AdminTestFlows.SetupTokenFromLog(factory), "Tobias", "admin@example.org");

        var (_, html) = await admin.Client.GetAsync("/Account");

        Assert.Contains("Noch kein Passkey", html, StringComparison.Ordinal);
        Assert.Contains("Neue Wiederherstellungscodes", html, StringComparison.Ordinal);
    }
}
