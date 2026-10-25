using BrowserGame.Server.Accounts;

namespace BrowserGame.Server.Tests;

public sealed class PlayerNamesDescribeTests
{
    private static readonly PlayerNameError[] Errors = [PlayerNameError.Length, PlayerNameError.Characters, PlayerNameError.Blocked];

    private static string[] Describe(string culture) => CultureScope.Run(culture, () => Errors.Select(PlayerNames.Describe).ToArray());

    [Fact]
    public void German_texts_are_exact() =>
        Assert.Equal(
        [
            "Der Name muss 3 bis 16 Zeichen lang sein.",
            "Der Name darf nur aus den Buchstaben A–Z bestehen (keine Umlaute, Ziffern, Leer- oder Sonderzeichen).",
            "Dieser Name ist nicht erlaubt.",
        ],
        Describe("de"));

    [Fact]
    public void English_texts_are_exact() =>
        Assert.Equal(
        [
            "The name must be 3 to 16 characters long.",
            "The name may only contain the letters A–Z (no accented letters, digits, spaces or special characters).",
            "This name is not allowed.",
        ],
        Describe("en"));

    [Theory]
    [InlineData("de")]
    [InlineData("en")]
    public void No_text_is_a_resource_key(string culture) =>
        Assert.All(Describe(culture), text => Assert.DoesNotContain("PlayerName.", text, StringComparison.Ordinal));

    [Fact]
    public void None_has_no_text() => Assert.Equal("", PlayerNames.Describe(PlayerNameError.None));
}
