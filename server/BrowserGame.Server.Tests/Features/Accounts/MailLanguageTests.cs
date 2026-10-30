using System.Net;
using BrowserGame.Data;
using BrowserGame.Server.Features.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using MimeKit;

namespace BrowserGame.Server.Tests;

/// <summary>006 T7: Mails an Spieler sind in der Sprache des Accounts, nicht der Anfrage.</summary>
public sealed class MailLanguageTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private FormClient ClientWith(string acceptLanguage)
    {
        var client = factory.CreateFormClient();
        client.Http.DefaultRequestHeaders.AcceptLanguage.Clear();
        client.Http.DefaultRequestHeaders.AcceptLanguage.ParseAdd(acceptLanguage);
        return client;
    }

    private async Task<AccountTestFlows.CreatedAccount> CreateAccountAsync(string language, bool confirmed = true)
    {
        var account = new AccountTestFlows.CreatedAccount(AccountTestFlows.UniqueEmail(), AccountTestFlows.UniqueName());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var result = await users.CreateAsync(
            new Account { UserName = account.Name, Email = account.Email, EmailConfirmed = confirmed, Language = language }, AccountTestFlows.Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    [Theory] // AK-9
    [InlineData("en", "Complete your sign-up", "Hello,", "The link is valid for 30 minutes.")]
    [InlineData("de", "Registrierung abschließen", "Hallo,", "Der Link ist 30 Minuten gültig.")]
    public async Task Registration_mail_uses_the_request_language(string acceptLanguage, string subject, string start, string lifetime)
    {
        using var client = ClientWith(acceptLanguage);
        var email = AccountTestFlows.UniqueEmail();

        await AccountTestFlows.RequestRegistrationAsync(client, email);

        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Equal(subject, mail.Subject);
        Assert.StartsWith(start, mail.Text, StringComparison.Ordinal);
        Assert.Contains(lifetime, mail.Text, StringComparison.Ordinal);
    }

    [Fact] // AK-9
    public async Task Registration_mail_follows_the_culture_cookie()
    {
        using var client = ClientWith("de");
        await client.PostAsync("/", "/Language", new Dictionary<string, string> { ["Language"] = "en", ["ReturnUrl"] = "/" });
        var email = AccountTestFlows.UniqueEmail();

        await AccountTestFlows.RequestRegistrationAsync(client, email);

        Assert.Equal("Complete your sign-up", Assert.Single(factory.Mails.To(email)).Subject);
    }

    [Theory] // AK-9
    [InlineData("en", "de", "You already have an account")]
    [InlineData("de", "en", "Du hast schon einen Account")]
    public async Task Already_registered_mail_uses_owner_language(string ownerLanguage, string registeringLanguage, string subject)
    {
        using var client = ClientWith(registeringLanguage);
        var owner = await CreateAccountAsync(ownerLanguage);

        await AccountTestFlows.RequestRegistrationAsync(client, owner.Email);

        var mail = Assert.Single(factory.Mails.To(owner.Email));
        Assert.Equal(subject, mail.Subject);
        Assert.Contains("/Account/Login", mail.Text, StringComparison.Ordinal);
        Assert.Contains("/Account/ForgotPassword", mail.Text, StringComparison.Ordinal);
    }

    [Fact] // AK-9
    public async Task Player_email_sender_sends_html_with_account_language()
    {
        await using var server = new FakeSmtpServer();
        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<SmtpSettingsStore>().SaveAsync(
            new SmtpSettings("127.0.0.1", server.Port, SmtpSecurity.None, server.Username, server.Password, "noreply@example.net", "Browser Game"),
            TestContext.Current.CancellationToken);
        var sender = new PlayerEmailSender(
            factory.Services.GetRequiredService<SmtpMailer>(), factory.Services.GetRequiredService<IWebHostEnvironment>(), NullLogger<PlayerEmailSender>.Instance);
        var account = new Account { UserName = "Tobias", Language = "en" };

        await sender.SendPasswordResetLinkAsync(account, "t@example.org", "https://game.test/c");

        using var raw = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(server.Messages)));
        var message = await MimeMessage.LoadAsync(raw, TestContext.Current.CancellationToken);
        Assert.Equal("Reset your password", message.Subject);
        Assert.Contains("<div lang=\"en\">", message.HtmlBody, StringComparison.Ordinal);
    }
}
