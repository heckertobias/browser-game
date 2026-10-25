using BrowserGame.Data;
using BrowserGame.Server.Localization;

namespace BrowserGame.Server.Tests;

public class GameLanguagesTests
{
    [Fact]
    public void Supported_is_en_and_de() => Assert.Equal(["en", "de"], GameLanguages.Supported);

    [Fact]
    public void Default_is_english_and_supported()
    {
        Assert.Equal("en", GameLanguages.Default);
        Assert.Contains(GameLanguages.Default, GameLanguages.Supported);
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("en", "en")]
    [InlineData("DE", "de")]
    [InlineData("En", "en")]
    [InlineData(" de ", "de")]
    public void Supported_codes_are_kept(string input, string expected) =>
        Assert.Equal(expected, GameLanguages.Normalize(input));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fr")]
    [InlineData("deu")]
    [InlineData("xx")]
    [InlineData("de-AT")]
    public void Unknown_or_missing_codes_fall_back_to_default(string? input) =>
        Assert.Equal("en", GameLanguages.Normalize(input));

    [Fact] // Bestand: Accounts ohne Angabe gelten als deutsch
    public void New_account_defaults_to_german() => Assert.Equal("de", new Account().Language);
}
