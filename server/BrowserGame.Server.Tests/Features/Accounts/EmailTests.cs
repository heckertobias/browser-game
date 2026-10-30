using BrowserGame.Server.Features.Accounts;
using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

public class EmailTests
{
    private sealed class Captured : GameEmailSender
    {
        public List<(string To, string Subject, string Text, string Html)> Mails { get; } = [];

        protected override Task SendAsync(string recipient, string subject, string text, string link, string language)
        {
            Mails.Add((recipient, subject, text, ToHtml(text, link, language)));
            return Task.CompletedTask;
        }
    }

    private static readonly Account Tobias = new() { UserName = "Tobias" };

    [Fact] // AK-2
    public async Task Registration_mail_is_german_and_contains_link()
    {
        var sender = new Captured();

        await sender.SendRegistrationLinkAsync("t@example.org", "de", "https://game.test/Account/CompleteRegistration?code=a&b=1", 30);

        var mail = Assert.Single(sender.Mails);
        Assert.Equal("t@example.org", mail.To);
        Assert.Contains("Registrierung", mail.Subject, StringComparison.Ordinal);
        Assert.StartsWith("Hallo,", mail.Text, StringComparison.Ordinal);
        Assert.Contains("https://game.test/Account/CompleteRegistration?code=a&b=1", mail.Text, StringComparison.Ordinal);
        Assert.Contains("30 Minuten", mail.Text, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://game.test/Account/CompleteRegistration?code=a&amp;b=1\">", mail.Html, StringComparison.Ordinal);
    }

    [Fact] // AK-14
    public async Task Reset_mail_mentions_single_use()
    {
        var sender = new Captured();

        await sender.SendPasswordResetLinkAsync(Tobias, "t@example.org", "https://game.test/reset");

        Assert.Contains("nur einmal", Assert.Single(sender.Mails).Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Html_escapes_user_controlled_name()
    {
        var sender = new Captured();

        await sender.SendPasswordResetLinkAsync(new Account { UserName = "<b>x</b>" }, "t@example.org", "https://game.test/c");

        Assert.DoesNotContain("<b>", Assert.Single(sender.Mails).Html, StringComparison.Ordinal);
    }
}
