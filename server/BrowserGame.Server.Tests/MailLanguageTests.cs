using System.Net;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
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

    [Fact] // AK-9
    public async Task Registration_in_english_sends_english_confirmation()
    {
        using var client = ClientWith("en");
        var email = AccountTestFlows.UniqueEmail();
        var name = AccountTestFlows.UniqueName();

        await AccountTestFlows.RegisterAsync(client, email, name);

        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Equal("Please confirm your email address", mail.Subject);
        Assert.StartsWith($"Hello {name[..1].ToUpperInvariant()}{name[1..].ToLowerInvariant()},", mail.Text, StringComparison.Ordinal);
        var (_, html) = await client.GetAsync(AccountTestFlows.LinkFromMail(mail.Text));
        Assert.Contains("Your email address is confirmed.", WebUtility.HtmlDecode(html), StringComparison.Ordinal);
    }

    [Fact] // AK-9
    public async Task Registration_in_german_sends_german_confirmation()
    {
        using var client = ClientWith("de");
        var email = AccountTestFlows.UniqueEmail();

        await AccountTestFlows.RegisterAsync(client, email, AccountTestFlows.UniqueName());

        var mail = Assert.Single(factory.Mails.To(email));
        Assert.Equal("Bitte bestätige deine E-Mail-Adresse", mail.Subject);
        Assert.StartsWith("Hallo ", mail.Text, StringComparison.Ordinal);
    }

    [Theory] // AK-9
    [InlineData("en", "de", null, "Please confirm your email address")]
    [InlineData("de", "en", null, "Bitte bestätige deine E-Mail-Adresse")]
    [InlineData("de", "de", "en", "Bitte bestätige deine E-Mail-Adresse")]
    public async Task Resend_uses_account_language_not_request_language(string accountLanguage, string acceptLanguage, string? cookieLanguage, string subject)
    {
        using var client = ClientWith(acceptLanguage);
        if (cookieLanguage is not null)
        {
            await client.PostAsync("/", "/Language", new Dictionary<string, string> { ["Language"] = cookieLanguage, ["ReturnUrl"] = "/" });
        }

        var account = await CreateAccountAsync(accountLanguage, confirmed: false);

        var (_, html) = await client.PostAsync("/Account/ResendConfirmation", "/Account/ResendConfirmation", new Dictionary<string, string> { ["Email"] = account.Email });

        if (cookieLanguage is not null)
        {
            Assert.Contains($"<html lang=\"{cookieLanguage}\"", html, StringComparison.Ordinal);
        }

        Assert.Equal(subject, Assert.Single(factory.Mails.To(account.Email)).Subject);
    }

    [Theory] // AK-9
    [InlineData("en", "de", "You already have an account")]
    [InlineData("de", "en", "Du hast schon einen Account")]
    public async Task Already_registered_mail_uses_owner_language(string ownerLanguage, string registeringLanguage, string subject)
    {
        using var client = ClientWith(registeringLanguage);
        var owner = await CreateAccountAsync(ownerLanguage);

        await AccountTestFlows.RegisterAsync(client, owner.Email, AccountTestFlows.UniqueName());

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

        await sender.SendConfirmationLinkAsync(account, "t@example.org", "https://game.test/c");

        using var raw = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Assert.Single(server.Messages)));
        var message = await MimeMessage.LoadAsync(raw, TestContext.Current.CancellationToken);
        Assert.Equal("Please confirm your email address", message.Subject);
        Assert.Contains("<div lang=\"en\">", message.HtmlBody, StringComparison.Ordinal);
    }
}
