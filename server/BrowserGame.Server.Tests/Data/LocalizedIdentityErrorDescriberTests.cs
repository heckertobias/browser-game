using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Tests;

public sealed class LocalizedIdentityErrorDescriberTests
{
    private static readonly LocalizedIdentityErrorDescriber Describer = new();

    private static IdentityError[] All() =>
    [
        Describer.DuplicateUserName("Tobias"),
        Describer.InvalidUserName("Tobias"),
        Describer.DuplicateEmail("a@example.org"),
        Describer.InvalidEmail("a"),
        Describer.PasswordTooShort(10),
        Describer.PasswordMismatch(),
        Describer.InvalidToken(),
        Describer.UserAlreadyHasPassword(),
        Describer.DefaultError(),
    ];

    private static string[] Descriptions(string culture) => CultureScope.Run(culture, () => All().Select(e => e.Description).ToArray());

    [Fact]
    public void German_texts_are_exact() =>
        Assert.Equal(
        [
            "Der Name „Tobias“ ist schon vergeben.",
            "Der Name enthält nicht erlaubte Zeichen.",
            "Die Registrierung hat nicht geklappt. Bitte versuche es erneut.",
            "Das ist keine gültige E-Mail-Adresse.",
            "Das Passwort muss mindestens 10 Zeichen lang sein.",
            "Das Passwort stimmt nicht.",
            "Der Link ist ungültig oder abgelaufen.",
            "Für diesen Account ist schon ein Passwort gesetzt.",
            "Etwas ist schiefgegangen. Bitte versuche es erneut.",
        ],
        Descriptions("de"));

    [Fact]
    public void English_texts_are_exact() =>
        Assert.Equal(
        [
            "The name “Tobias” is already taken.",
            "The name contains characters that are not allowed.",
            "Registration did not work. Please try again.",
            "That is not a valid email address.",
            "The password must be at least 10 characters long.",
            "The password is incorrect.",
            "The link is invalid or has expired.",
            "This account already has a password.",
            "Something went wrong. Please try again.",
        ],
        Descriptions("en"));

    [Fact]
    public void Code_is_the_method_name() =>
        Assert.Equal(
            ["DuplicateUserName", "InvalidUserName", "DuplicateEmail", "InvalidEmail", "PasswordTooShort", "PasswordMismatch", "InvalidToken", "UserAlreadyHasPassword", "DefaultError"],
            All().Select(e => e.Code));

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void Duplicate_email_does_not_reveal_the_address(string culture)
    {
        var (first, second) = CultureScope.Run(culture, () => (Describer.DuplicateEmail("eins@example.org"), Describer.DuplicateEmail("zwei@example.org")));

        Assert.Equal(first.Description, second.Description);
        Assert.DoesNotContain("example.org", first.Description, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("fr")]
    public void Other_languages_fall_back_to_english(string culture) =>
        Assert.Equal(Descriptions("en"), Descriptions(culture));
}
