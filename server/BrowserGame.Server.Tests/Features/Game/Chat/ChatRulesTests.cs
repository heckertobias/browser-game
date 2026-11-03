using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class ChatRulesTests
{
    private const char LineSeparator = (char)0x2028;
    private const char ParagraphSeparator = (char)0x2029;
    private const char NoBreakSpace = (char)0x00A0;
    private const char IdeographicSpace = (char)0x3000;

    private static string? Normalize(string text) => ChatRules.Normalize(text, GameSettings.ChatMaxLength);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("     ")]
    [InlineData("\t")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData(" \n \r\n\t ")]
    public void Blank_text_is_rejected(string text)
    {
        Assert.Null(Normalize(text));
    }

    [Fact]
    public void Text_of_only_control_characters_is_rejected()
    {
        foreach (var c in "\0\u001F\u007F\u0085\u009F")
        {
            Assert.Null(Normalize(c.ToString()));
            Assert.Null(Normalize($"{c} {c}"));
        }
    }

    [Fact]
    public void Text_of_only_unicode_whitespace_is_rejected()
    {
        foreach (var c in new[] { LineSeparator, ParagraphSeparator, NoBreakSpace, IdeographicSpace })
        {
            Assert.Null(Normalize(c.ToString()));
        }
    }

    [Fact]
    public void Text_of_200_units_is_kept()
    {
        var text = new string('a', 200);

        Assert.Equal(text, Normalize(text));
    }

    [Fact]
    public void Text_of_201_units_is_rejected()
    {
        Assert.Null(Normalize(new string('a', 201)));
    }

    [Fact]
    public void Length_is_counted_after_trimming()
    {
        var a200 = new string('a', 200);

        foreach (var text in new[] { $"  {a200}  ", $"{a200}\n", $"\t{a200}\r\n", $"{a200}\0" })
        {
            Assert.Equal(a200, Normalize(text));
        }
    }

    [Fact]
    public void Too_long_stays_too_long_with_surrounding_spaces()
    {
        Assert.Null(Normalize($"  {new string('a', 201)}  "));
    }

    [Fact]
    public void Inner_control_characters_count_towards_the_length()
    {
        Assert.Null(Normalize(new string('a', 100) + "\n" + new string('a', 100)));
        Assert.Equal(
            new string('a', 99) + " " + new string('a', 100),
            Normalize(new string('a', 99) + "\n" + new string('a', 100)));
    }

    [Fact]
    public void Length_is_counted_in_utf16_units()
    {
        var emojis = string.Concat(Enumerable.Repeat("\U0001F600", 100));

        Assert.Equal(emojis, Normalize(emojis));
        Assert.Null(Normalize(emojis + "a"));
    }

    [Fact]
    public void Control_characters_become_spaces()
    {
        foreach (var c in "\0\t\n\r\u001F\u007F\u0085\u009F" + LineSeparator + ParagraphSeparator)
        {
            Assert.Equal("a b", Normalize($"a{c}b"));
        }
    }

    [Fact]
    public void Each_control_character_becomes_its_own_space()
    {
        Assert.Equal("a  b", Normalize("a\r\nb"));
    }

    [Fact]
    public void Surrounding_whitespace_is_trimmed()
    {
        Assert.Equal("hi", Normalize("  hi \n"));
    }

    [Fact]
    public void Inner_spaces_are_kept()
    {
        Assert.Equal("a   b", Normalize("a   b"));
    }

    [Theory]
    [InlineData("<b>hi</b>")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("https://example.com/?a=1&b=2")]
    [InlineData("www.example.com")]
    [InlineData("a & b &amp; c")]
    [InlineData("\"q\" 'x'")]
    [InlineData("/help")]
    [InlineData("[x](y)")]
    [InlineData("Äöü 你好 \U0001F600")]
    public void Markup_and_links_stay_unchanged(string text)
    {
        Assert.Equal(text, Normalize(text));
    }
}
