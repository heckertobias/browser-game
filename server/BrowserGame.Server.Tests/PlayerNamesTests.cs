using BrowserGame.Server.Accounts;

namespace BrowserGame.Server.Tests;

public class PlayerNamesTests
{
    [Theory] // AK-6
    [InlineData("tObIaS", "Tobias")]
    [InlineData("TOBIAS", "Tobias")]
    [InlineData("tobias", "Tobias")]
    [InlineData("  Elwyn  ", "Elwyn")]
    [InlineData("Ann", "Ann")]
    [InlineData("Abcdefghijklmnop", "Abcdefghijklmnop")]
    public void Valid_names_are_normalized(string input, string expected)
    {
        Assert.Equal(PlayerNameError.None, PlayerNames.TryNormalize(input, out var name));
        Assert.Equal(expected, name);
    }

    [Theory] // AK-5
    [InlineData("")]
    [InlineData(null)]
    [InlineData("Al")]
    [InlineData("Abcdefghijklmnopq")] // 17
    public void Wrong_length_is_rejected(string? input)
    {
        Assert.Equal(PlayerNameError.Length, PlayerNames.TryNormalize(input, out var name));
        Assert.Empty(name);
    }

    [Theory] // AK-5
    [InlineData("Jörg")]
    [InlineData("Tobi2")]
    [InlineData("Tobi Hecker")]
    [InlineData("Tobi_H")]
    [InlineData("Tobi-H")]
    [InlineData("Ⅽaesar")] // römische Ziffer statt C
    [InlineData("Таня")]   // Kyrillisch
    public void Other_characters_are_rejected(string input)
    {
        Assert.Equal(PlayerNameError.Characters, PlayerNames.TryNormalize(input, out _));
    }

    [Theory] // AK-7b
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Moderator")]
    [InlineData("system")]
    public void Blocked_names_are_rejected_regardless_of_case(string input)
    {
        Assert.Equal(PlayerNameError.Blocked, PlayerNames.TryNormalize(input, out _));
    }

    [Fact] // Sperrliste prüft nur ganze Namen (Wortteil-Erkennung ist Out of Scope)
    public void Names_containing_blocked_words_are_allowed_for_now()
    {
        Assert.Equal(PlayerNameError.None, PlayerNames.TryNormalize("Adminton", out _));
    }

    [Fact]
    public void Every_error_has_a_description()
    {
        foreach (var error in Enum.GetValues<PlayerNameError>().Where(e => e != PlayerNameError.None))
        {
            Assert.NotEmpty(PlayerNames.Describe(error));
        }
    }
}
