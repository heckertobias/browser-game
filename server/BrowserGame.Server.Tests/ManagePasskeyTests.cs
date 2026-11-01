using System.Text.Json;

namespace BrowserGame.Server.Tests;

/// <summary>003 T9b: Passkey in Manage hinzufügen (AK-13b).</summary>
public sealed class ManagePasskeyTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    [Fact] // AK-13b
    public async Task Options_exclude_existing_passkeys_and_use_email_as_user_name()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var id = await AccountTestFlows.AddStoredPasskeyAsync(factory, account.Email);

        var (_, html) = await client.GetAsync("/Account/Manage");

        Assert.Contains("data-passkey=\"create\"", html, StringComparison.Ordinal);
        using var options = JsonDocument.Parse(AccountTestFlows.PasskeyOptions(html));
        var excluded = options.RootElement.GetProperty("excludeCredentials").EnumerateArray().Select(e => e.GetProperty("id").GetString());
        Assert.Contains(AccountTestFlows.PasskeyId(id), excluded);
        Assert.Equal(account.Email, options.RootElement.GetProperty("user").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Garbage_credential_is_rejected_and_adds_nothing()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        var (_, page) = await client.GetAsync("/Account/Manage");

        foreach (var credential in new[] { "", "kein json", "{}" })
        {
            var (_, html) = await client.PostWithTokenAsync(
                "/Account/Manage?handler=AddPasskey", new Dictionary<string, string> { ["Credential"] = credential }, FormClient.AntiforgeryToken(page));

            Assert.Contains("Der Passkey konnte nicht hinzugefügt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        }

        Assert.Equal(0, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact]
    public async Task Same_authenticator_cannot_be_added_twice()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        using var authenticator = new SoftwareAuthenticator();
        await AccountTestFlows.AddPasskeyAsync(client, authenticator);

        var (_, html) = await AccountTestFlows.AddPasskeyAsync(client, authenticator);

        Assert.Contains("Der Passkey konnte nicht hinzugefügt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }

    [Fact] // AK-13b
    public async Task Two_devices_add_remove_one_sign_in_with_other()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        using var first = new SoftwareAuthenticator();
        using var second = new SoftwareAuthenticator();
        await AccountTestFlows.AddPasskeyAsync(client, first);
        var (_, added) = await AccountTestFlows.AddPasskeyAsync(client, second);
        Assert.Contains("Passkey hinzugefügt.", HtmlTexts.Visible(added), StringComparison.Ordinal);
        Assert.Equal(2, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));

        await AccountTestFlows.RemovePasskeyAsync(client, first.Id);

        Assert.Equal(1, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
        using var fresh = factory.CreateFormClient();
        var (rejected, _) = await AccountTestFlows.PasskeyLoginAsync(fresh, first);
        var (accepted, _) = await AccountTestFlows.PasskeyLoginAsync(fresh, second);
        Assert.Equal(System.Net.HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(System.Net.HttpStatusCode.Redirect, accepted.StatusCode);
    }

    [Fact]
    public async Task Wrong_ceremony_state_is_rejected_cleanly()
    {
        var (client, account) = await AccountTestFlows.SignInAsync(factory);
        using var _ = client;
        using var authenticator = new SoftwareAuthenticator();
        var (_, page) = await client.GetAsync("/Account/Manage");
        await client.GetAsync("/Account/Login"); // ersetzt den Zeremonie-Zustand durch Anmelde-Optionen

        var (response, html) = await client.PostWithTokenAsync(
            "/Account/Manage?handler=AddPasskey",
            new Dictionary<string, string> { ["Credential"] = authenticator.Create(AccountTestFlows.PasskeyOptions(page)) },
            FormClient.AntiforgeryToken(page));

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Der Passkey konnte nicht hinzugefügt werden.", HtmlTexts.Visible(html), StringComparison.Ordinal);
        Assert.Equal(0, await AccountTestFlows.PasskeyCountAsync(factory, account.Email));
    }
}
