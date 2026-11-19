using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class CommandParserTests
{
    private static ParseResult.Ok AssertOk(string text, string expectedName)
    {
        var ok = Assert.IsType<ParseResult.Ok>(CommandParser.Parse(text));
        Assert.Equal(expectedName, ok.Name);
        return ok;
    }

    [Theory]
    [InlineData("/run fullHealth", "fullHealth")]
    [InlineData("/run fullHealth()", "fullHealth")]
    [InlineData("/run  fullHealth ( )", "fullHealth")]
    [InlineData("/run resetPosition()", "resetPosition")]
    [InlineData("/run fullHealth() ", "fullHealth")]
    [InlineData("/run x1()", "x1")]
    [InlineData("/run a", "a")]
    [InlineData("/run\u00A0fullHealth()", "fullHealth")]
    [InlineData("/run\u3000fullHealth ()", "fullHealth")]
    public void Run_without_parameters_is_parsed(string text, string expectedName)
    {
        Assert.Empty(AssertOk(text, expectedName).Args);
    }

    [Theory]
    [InlineData("/run fullhealth()", "fullhealth")]
    [InlineData("/run FULLHEALTH()", "FULLHEALTH")]
    public void Name_keeps_the_typed_case(string text, string expectedName)
    {
        Assert.Empty(AssertOk(text, expectedName).Args);
    }

    [Theory]
    [InlineData("/Run fullHealth()")]
    [InlineData("/RUN fullHealth()")]
    public void Run_prefix_ignores_case(string text)
    {
        Assert.Empty(AssertOk(text, "fullHealth").Args);
    }

    [Fact]
    public void Parameters_are_split_in_order()
    {
        string[] expected = ["A", "B", "C"];
        Assert.Equal(expected, AssertOk("/run debug(A, B, C)", "debug").Args);
    }

    [Fact]
    public void Parameters_are_trimmed()
    {
        string[] expected = ["A", "B", "C"];
        Assert.Equal(expected, AssertOk("/run debug(  A ,B  ,  C )", "debug").Args);
    }

    [Fact]
    public void Inner_spaces_of_a_parameter_are_kept()
    {
        string[] expected = ["A  B"];
        Assert.Equal(expected, AssertOk("/run debug(A  B)", "debug").Args);
    }

    [Fact]
    public void Parameters_may_contain_any_other_characters()
    {
        string[] expected = ["ä", "你好", "-1.5", "(x"];
        Assert.Equal(expected, AssertOk("/run debug(ä, 你好, -1.5, (x)", "debug").Args);
    }

    [Fact]
    public void Parameter_count_is_not_checked_by_the_parser()
    {
        string[] expected = ["1"];
        Assert.Equal(expected, AssertOk("/run fullHealth(1)", "fullHealth").Args);
    }

    [Fact]
    public void Unknown_names_are_not_checked_by_the_parser()
    {
        Assert.Empty(AssertOk("/run nope()", "nope").Args);
    }

    [Theory]
    [InlineData("/xyz")]
    [InlineData("/xyz()")]
    [InlineData("/")]
    [InlineData("/ run fullHealth()")]
    [InlineData("/runfullHealth()")]
    [InlineData("/run1 fullHealth()")]
    [InlineData("/run()")]
    [InlineData("/run(x)")]
    [InlineData("/runfullHealth")]
    [InlineData("/run,x")]
    [InlineData("/running fullHealth()")]
    [InlineData("/debug full_health")]
    [InlineData("/help")]
    [InlineData("//run fullHealth()")]
    [InlineData("hello")]
    [InlineData("run fullHealth()")]
    public void Other_prefixes_are_unknown(string text)
    {
        Assert.IsType<ParseResult.UnknownPrefix>(CommandParser.Parse(text));
    }

    [Theory]
    [InlineData("/run")]
    [InlineData("/RUN")]
    [InlineData("/run ")]
    [InlineData("/run   ")]
    [InlineData("/run ()")]
    [InlineData("/run (x)")]
    [InlineData("/run 1x()")]
    [InlineData("/run _x()")]
    [InlineData("/run full_health")]
    [InlineData("/run full-health()")]
    [InlineData("/run fülle()")]
    [InlineData("/run \u00E4rger()")]
    [InlineData("/run x\u0663()")]
    [InlineData("/run x y")]
    [InlineData("/run x)")]
    [InlineData("/run x;")]
    [InlineData("/run x(")]
    [InlineData("/run x(a")]
    [InlineData("/run x(a,")]
    [InlineData("/run x() y")]
    [InlineData("/run x()()")]
    [InlineData("/run x();")]
    [InlineData("/run x(a)b")]
    [InlineData("/run x((a))")]
    [InlineData("/run x(,)")]
    [InlineData("/run x(a,)")]
    [InlineData("/run x(,a)")]
    [InlineData("/run x(a,,b)")]
    [InlineData("/run x(a, ,b)")]
    public void Broken_run_commands_are_invalid(string text)
    {
        Assert.IsType<ParseResult.Invalid>(CommandParser.Parse(text));
    }
}
