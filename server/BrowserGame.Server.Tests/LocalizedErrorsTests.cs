using System.Net;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

/// <summary>006: Fehlertexte der Registrierung folgen der Sprache der Anfrage.</summary>
public sealed class LocalizedErrorsTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private FormClient EnglishClient()
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd("en");
        return client;
    }

    [Fact]
    public async Task Invalid_characters_are_explained_in_english()
    {
        using var client = EnglishClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), "Ab1");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("The name may only contain the letters A–Z", text, StringComparison.Ordinal);
        Assert.DoesNotContain("nur aus den Buchstaben", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Wrong_length_is_explained_in_english()
    {
        using var client = EnglishClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), "Al");

        Assert.Contains("The name must be 3 to 16 characters long.", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Short_password_is_explained_in_english()
    {
        using var client = EnglishClient();

        var (_, html) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName(), "kurz");
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("The password must be at least 10 characters long.", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Das Passwort muss", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Taken_name_is_reported_in_english()
    {
        using var client = EnglishClient();
        var first = await AccountTestFlows.CreateConfirmedAccountAsync(factory);

        var (_, html) = await AccountTestFlows.RegisterAsync(client, AccountTestFlows.UniqueEmail(), first.Name);
        var text = WebUtility.HtmlDecode(html);

        Assert.Contains("is already taken", text, StringComparison.Ordinal);
        Assert.DoesNotContain("schon vergeben", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Existing_email_looks_like_success_in_english_and_warns_the_owner()
    {
        using var client = EnglishClient();
        var existing = await AccountTestFlows.CreateConfirmedAccountAsync(factory);
        using var other = EnglishClient();
        var (_, forNew) = await AccountTestFlows.RegisterAsync(other, AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());

        var (_, html) = await AccountTestFlows.RegisterAsync(client, existing.Email, AccountTestFlows.UniqueName());

        Assert.Contains("/Account/ResendConfirmation", forNew, StringComparison.Ordinal);
        Assert.Contains("/Account/ResendConfirmation", html, StringComparison.Ordinal);
        Assert.DoesNotContain("Registration did not work", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
        Assert.Single(factory.Mails.To(existing.Email));
    }

    [Fact] // AK-11
    public async Task Registration_in_english_normalizes_the_name()
    {
        using var client = EnglishClient();
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();

        await AccountTestFlows.RegisterAsync(client, email, name.ToUpperInvariant());

        await using var scope = factory.Services.CreateAsyncScope();
        var account = await scope.ServiceProvider.GetRequiredService<UserManager<Account>>().FindByEmailAsync(email);
        Assert.NotNull(account);
        Assert.Equal(name, account.UserName);
    }
}
