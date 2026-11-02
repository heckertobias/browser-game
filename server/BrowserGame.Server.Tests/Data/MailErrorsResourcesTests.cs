using System.Collections;
using System.Globalization;
using System.Net.Sockets;
using System.Reflection;
using BrowserGame.Data;
using MailKit.Net.Smtp;
using MailKit.Security;

namespace BrowserGame.Server.Tests;

public sealed class MailErrorsResourcesTests
{
    private static Dictionary<string, string> Neutral() =>
        MailErrors.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false) is { } set
            ? set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!)
            : [];

    [Fact] // 005 AK-14
    public void Neutral_texts_exist_and_none_is_empty()
    {
        var texts = Neutral();

        Assert.NotEmpty(texts);
        Assert.All(texts, e => Assert.False(string.IsNullOrWhiteSpace(e.Value), e.Key));
    }

    [Fact] // 005 AK-14
    public void Keys_are_exactly_the_members()
    {
        var members = typeof(MailErrors).GetProperties(BindingFlags.Public | BindingFlags.Static).Select(p => p.Name)
            .Concat(typeof(MailErrors).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(m => !m.IsSpecialName).Select(m => m.Name))
            .ToList();
        var keys = Neutral().Keys;

        Assert.True(
            !members.Except(keys).Any() && !keys.Except(members).Any(),
            $"Ohne Text: [{string.Join(", ", members.Except(keys))}]; ohne Member: [{string.Join(", ", keys.Except(members))}]");
    }

    [Fact] // 005 AK-14
    public void Describe_uses_the_resource_texts()
    {
        var texts = Neutral();

        Assert.Equal(texts["AuthenticationFailed"], SmtpMailer.Describe(new AuthenticationException("x"), "mx.example"));
        Assert.Equal(texts["Unreachable"], SmtpMailer.Describe(new SocketException(), "mx.example"));
        Assert.Equal(
            string.Format(CultureInfo.InvariantCulture, texts["Rejected"], "nope"),
            SmtpMailer.Describe(new SmtpCommandException(SmtpErrorCode.MessageNotAccepted, SmtpStatusCode.TransactionFailed, "nope"), "mx.example"));
        Assert.Equal(texts["ConnectionLost"], SmtpMailer.Describe(new IOException(), "mx.example"));
        Assert.Null(SmtpMailer.Describe(new InvalidOperationException(), "mx.example"));
    }

    [Theory] // 005 AK-14
    [InlineData("de")]
    [InlineData("fr")]
    public void Other_languages_fall_back_to_english(string culture) =>
        Assert.Equal(Neutral()["AuthenticationFailed"], CultureScope.Run(culture, () => MailErrors.AuthenticationFailed));
}
