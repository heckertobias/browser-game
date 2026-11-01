using BrowserGame.Data;
using BrowserGame.Server.Features.Accounts;

namespace BrowserGame.Server.Tests;

/// <summary>006 T7: Mailtexte je Sprache des Accounts.</summary>
public class EmailLanguageTests
{
    private const string Link = "https://game.test/Account/CompleteRegistration?code=a&b=1";
    private const string Login = "https://game.test/Account/Login";
    private const string Forgot = "https://game.test/Account/ForgotPassword";

    private sealed class Captured : GameEmailSender
    {
        public List<(string To, string Subject, string Text, string Html, string Language)> Mails { get; } = [];

        protected override Task SendAsync(string recipient, string subject, string text, string link, string language)
        {
            Mails.Add((recipient, subject, text, ToHtml(text, link, language), language));
            return Task.CompletedTask;
        }
    }

    private static Account AccountWith(string language, string name = "Tobias") => new() { UserName = name, Language = language };

    private static async Task<(string To, string Subject, string Text, string Html, string Language)[]> AllMailsAsync(Account account)
    {
        var sender = new Captured();
        await sender.SendRegistrationLinkAsync("t@example.org", account.Language, Link, 30);
        await sender.SendPasswordResetLinkAsync(account, "t@example.org", Link);
        await sender.SendAlreadyRegisteredAsync(account, "t@example.org", Login, Forgot);
        return [.. sender.Mails];
    }

    [Fact] // AK-9
    public async Task German_mails_are_unchanged()
    {
        var mails = await AllMailsAsync(AccountWith("de"));

        Assert.Equal("Registrierung abschließen", mails[0].Subject);
        Assert.Equal(
            $"Hallo,\n\nüber diesen Link wählst du deinen Spielernamen und legst deinen Account an:\n{Link}\n\n"
            + "Der Link ist 30 Minuten gültig. Wenn du dich nicht registriert hast, kannst du diese Mail ignorieren.",
            mails[0].Text);
        Assert.Equal("Passwort zurücksetzen", mails[1].Subject);
        Assert.Equal(
            $"Hallo Tobias,\n\nüber diesen Link kannst du ein neues Passwort setzen:\n{Link}\n\n"
            + "Der Link ist 3 Stunden gültig und funktioniert nur einmal. Wenn du das nicht angefordert hast, ignoriere diese Mail.",
            mails[1].Text);
        Assert.Equal("Du hast schon einen Account", mails[2].Subject);
        Assert.Equal(
            "Hallo Tobias,\n\njemand hat versucht, mit deiner E-Mail-Adresse einen neuen Account anzulegen. "
            + $"Du hast bereits einen. Hier kannst du dich anmelden:\n{Login}\n\n"
            + $"Passwort vergessen? {Forgot}\n\nWenn du das nicht warst, musst du nichts tun.",
            mails[2].Text);
    }

    [Fact] // AK-9
    public async Task English_mails_are_exact()
    {
        var mails = await AllMailsAsync(AccountWith("en"));

        Assert.Equal("Complete your sign-up", mails[0].Subject);
        Assert.Equal(
            $"Hello,\n\nUse this link to choose your player name and create your account:\n{Link}\n\n"
            + "The link is valid for 30 minutes. If you didn't sign up, you can ignore this email.",
            mails[0].Text);
        Assert.Equal("Reset your password", mails[1].Subject);
        Assert.Equal(
            $"Hello Tobias,\n\nUse this link to set a new password:\n{Link}\n\n"
            + "The link is valid for 3 hours and works only once. If you didn't request this, ignore this email.",
            mails[1].Text);
        Assert.Equal("You already have an account", mails[2].Subject);
        Assert.Equal(
            "Hello Tobias,\n\nSomeone tried to create a new account with your email address. "
            + $"You already have one. You can sign in here:\n{Login}\n\n"
            + $"Forgot your password? {Forgot}\n\nIf this wasn't you, you don't need to do anything.",
            mails[2].Text);
    }

    [Theory] // AK-9: die Kultur der Anfrage zählt nicht
    [InlineData("de", "en", "Reset your password")]
    [InlineData("en", "de", "Passwort zurücksetzen")]
    public async Task Language_comes_from_the_account_not_the_current_culture(string requestCulture, string accountLanguage, string subject)
    {
        var sender = new Captured();

        await CultureScope.Run(requestCulture, () => sender.SendPasswordResetLinkAsync(AccountWith(accountLanguage), "t@example.org", Link));

        Assert.Equal(subject, Assert.Single(sender.Mails).Subject);
    }

    [Theory]
    [InlineData("xx", "en")]
    [InlineData("", "en")]
    [InlineData("de-AT", "en")]
    [InlineData("DE", "de")]
    public async Task Unsupported_language_falls_back_to_english(string stored, string expected)
    {
        var mails = await AllMailsAsync(AccountWith(stored));

        Assert.All(mails, m => Assert.Equal(expected, m.Language));
        Assert.Equal(expected == "de" ? "Passwort zurücksetzen" : "Reset your password", mails[1].Subject);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public async Task Html_has_lang_of_the_account(string language)
    {
        var mails = await AllMailsAsync(AccountWith(language));

        Assert.All(mails, m => Assert.StartsWith($"<div lang=\"{language}\">", m.Html, StringComparison.Ordinal));
        Assert.Contains("<a href=\"https://game.test/Account/CompleteRegistration?code=a&amp;b=1\">", mails[0].Html, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public async Task Html_escapes_user_controlled_name_in_both_languages(string language)
    {
        var mails = await AllMailsAsync(AccountWith(language, "<b>x</b>"));

        Assert.All(mails, m => Assert.DoesNotContain("<b>", m.Html, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public async Task Every_link_is_followed_by_a_line_break_or_end(string language)
    {
        var mails = await AllMailsAsync(AccountWith(language));

        Assert.Equal(new Uri(Link).PathAndQuery, AccountTestFlows.LinkFromMail(mails[0].Text));
        Assert.Equal(new Uri(Link).PathAndQuery, AccountTestFlows.LinkFromMail(mails[1].Text));
        Assert.Equal(new Uri(Login).PathAndQuery, AccountTestFlows.LinkFromMail(mails[2].Text));
        Assert.Contains($"\n{Login}\n", mails[2].Text, StringComparison.Ordinal);
        Assert.EndsWith(Forgot, mails[2].Text.Split("\n\n")[2].TrimEnd(), StringComparison.Ordinal);
        foreach (var mail in mails.Take(2))
        {
            Assert.Contains($"\n{Link}\n", mail.Text, StringComparison.Ordinal);
        }
    }
}
