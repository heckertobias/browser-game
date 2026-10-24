using BrowserGame.Server.Accounts;
using BrowserGame.Server.Data;

namespace BrowserGame.Server.Tests;

public class EmailTests
{
    private sealed class Captured : GameEmailSender
    {
        public List<(string To, string Subject, string Text, string Html)> Mails { get; } = [];

        protected override Task SendAsync(string to, string subject, string text, string link)
        {
            Mails.Add((to, subject, text, ToHtml(text, link)));
            return Task.CompletedTask;
        }
    }

    private static readonly Account Tobias = new() { UserName = "Tobias" };

    [Fact] // AK-2
    public async Task Confirmation_mail_is_german_and_contains_link()
    {
        var sender = new Captured();

        await sender.SendConfirmationLinkAsync(Tobias, "t@example.org", "https://game.test/Account/ConfirmEmail?code=a&b=1");

        var mail = Assert.Single(sender.Mails);
        Assert.Equal("t@example.org", mail.To);
        Assert.Contains("bestätige", mail.Subject, StringComparison.Ordinal);
        Assert.Contains("Hallo Tobias", mail.Text, StringComparison.Ordinal);
        Assert.Contains("https://game.test/Account/ConfirmEmail?code=a&b=1", mail.Text, StringComparison.Ordinal);
        Assert.Contains("<a href=\"https://game.test/Account/ConfirmEmail?code=a&amp;b=1\">", mail.Html, StringComparison.Ordinal);
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

        await sender.SendConfirmationLinkAsync(new Account { UserName = "<b>x</b>" }, "t@example.org", "https://game.test/c");

        Assert.DoesNotContain("<b>", Assert.Single(sender.Mails).Html, StringComparison.Ordinal);
    }
}
