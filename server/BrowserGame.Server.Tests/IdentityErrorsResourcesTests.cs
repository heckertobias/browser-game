using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using BrowserGame.Server.Localization;

namespace BrowserGame.Server.Tests;

public sealed partial class IdentityErrorsResourcesTests
{
    private const string IdentityErrors = "BrowserGame.Data.Resources.IdentityErrors";

    private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de");

    private static ResourceManager Manager(string baseName) =>
        baseName == IdentityErrors ? LocalizedIdentityErrorDescriber.ResourceManager : Texts.ResourceManager;

    private static Dictionary<string, string> Load(string baseName, CultureInfo culture) =>
        Manager(baseName).GetResourceSet(culture, createIfNotExists: true, tryParents: false) is { } set
            ? set.Cast<DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!)
            : [];

    [Fact]
    public void Both_resource_sets_exist_and_are_not_empty()
    {
        Assert.NotEmpty(Load(IdentityErrors, CultureInfo.InvariantCulture));
        Assert.NotEmpty(Load(IdentityErrors, German));
    }

    [Fact]
    public void Neutral_and_german_have_the_same_keys()
    {
        var neutral = Load(IdentityErrors, CultureInfo.InvariantCulture).Keys;
        var german = Load(IdentityErrors, German).Keys;

        Assert.True(
            !neutral.Except(german).Any() && !german.Except(neutral).Any(),
            $"Fehlt in de: [{string.Join(", ", neutral.Except(german))}]; fehlt in en: [{string.Join(", ", german.Except(neutral))}]");
    }

    [Fact]
    public void No_value_is_empty()
    {
        var empty = new[] { CultureInfo.InvariantCulture, German }
            .SelectMany(c => Load(IdentityErrors, c).Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{c.Name}:{e.Key}"));

        Assert.Empty(empty);
    }

    [Fact]
    public void Keys_are_exactly_the_overridden_describer_methods()
    {
        var methods = typeof(LocalizedIdentityErrorDescriber)
            .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Public | BindingFlags.Instance)
            .Select(m => m.Name)
            .ToList();
        var keys = Load(IdentityErrors, CultureInfo.InvariantCulture).Keys;

        Assert.True(
            !methods.Except(keys).Any() && !keys.Except(methods).Any(),
            $"Ohne Text: [{string.Join(", ", methods.Except(keys))}]; ohne Methode: [{string.Join(", ", keys.Except(methods))}]");
    }

    [Theory]
    [InlineData(IdentityErrors)]
    [InlineData(Texts.BaseName)]
    public void Placeholders_match_between_english_and_german(string baseName)
    {
        var neutral = Load(baseName, CultureInfo.InvariantCulture);
        var german = Load(baseName, German);

        foreach (var (key, value) in neutral)
        {
            Assert.Equal(Placeholders(value), Placeholders(german[key]));
        }
    }

    private static string[] Placeholders(string text) =>
        [.. Placeholder().Matches(text).Select(m => m.Value).Distinct().Order()];

    [GeneratedRegex(@"\{\d+\}")]
    private static partial Regex Placeholder();
}
