using BrowserGame.Server.Features.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;

namespace BrowserGame.Server.Tests;

public sealed class LanguageOnlyCultureProviderTests
{
    private static Task<ProviderCultureResult?> Run(ProviderCultureResult? inner) =>
        new LanguageOnlyCultureProvider(new CustomRequestCultureProvider(_ => Task.FromResult(inner)))
            .DetermineProviderCultureResult(new DefaultHttpContext());

    [Fact] // AK-1
    public async Task No_result_stays_no_result() => Assert.Null(await Run(null));

    [Theory] // AK-1
    [InlineData("de-AT", "de")]
    [InlineData("de-DE", "de")]
    [InlineData("DE-at", "DE")]
    [InlineData("fr-FR", "fr")]
    [InlineData("zh-Hant-TW", "zh")]
    [InlineData("de", "de")]
    [InlineData("*", "*")]
    [InlineData("", "")]
    [InlineData("-AT", "-AT")]
    public async Task Names_are_cut_to_the_language(string name, string expected)
    {
        var result = await Run(new ProviderCultureResult(name));

        Assert.Equal(expected, Assert.Single(result!.Cultures).Value ?? "");
        Assert.Equal(expected, Assert.Single(result.UICultures).Value ?? "");
    }

    [Fact] // AK-1
    public async Task Culture_and_ui_culture_are_cut_independently()
    {
        var result = await Run(new ProviderCultureResult("de-AT", "en-US"));

        Assert.Equal("de", Assert.Single(result!.Cultures).Value);
        Assert.Equal("en", Assert.Single(result.UICultures).Value);
    }

    [Fact] // AK-1
    public async Task Order_is_kept()
    {
        var result = await Run(new ProviderCultureResult(
            new List<StringSegment> { "fr-FR", "de-AT", "en" }, new List<StringSegment> { "fr-FR", "de-AT", "en" }));

        Assert.Equal(["fr", "de", "en"], result!.Cultures.Select(c => c.Value));
    }
}
