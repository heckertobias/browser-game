using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using System.Resources;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Localization.Resources;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BrowserGame.Server.Tests;

public sealed class TextsResourcesTests
{
    private const string BaseName = "BrowserGame.Server.Features.Localization.Resources.Texts";

    private static readonly ResourceManager Manager = new(BaseName, typeof(Texts).Assembly);

    private static Dictionary<string, string> Load(CultureInfo culture) =>
        Manager.GetResourceSet(culture, createIfNotExists: true, tryParents: false) is { } set
            ? set.Cast<System.Collections.DictionaryEntry>().ToDictionary(e => (string)e.Key, e => (string)e.Value!)
            : [];

    [Fact] // AK-10
    public void Both_resource_sets_exist_and_are_not_empty()
    {
        Assert.NotEmpty(Load(CultureInfo.InvariantCulture));
        Assert.NotEmpty(Load(CultureInfo.GetCultureInfo("de")));
    }

    [Fact] // AK-10
    public void Neutral_and_german_have_the_same_keys()
    {
        var neutral = Load(CultureInfo.InvariantCulture).Keys;
        var german = Load(CultureInfo.GetCultureInfo("de")).Keys;

        Assert.True(
            !neutral.Except(german).Any() && !german.Except(neutral).Any(),
            $"Fehlt in de: [{string.Join(", ", neutral.Except(german))}]; fehlt in en: [{string.Join(", ", german.Except(neutral))}]");
    }

    [Fact] // AK-10
    public void No_value_is_empty()
    {
        var empty = new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("de") }
            .SelectMany(c => Load(c).Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => $"{c.Name}:{e.Key}"));

        Assert.Empty(empty);
    }

    [Fact] // AK-10
    public void Validation_messages_are_resource_keys()
    {
        var keys = Load(CultureInfo.InvariantCulture).Keys;
        var messages = typeof(Texts).Assembly.GetTypes()
            .Where(t => typeof(PageModel).IsAssignableFrom(t))
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance))
            .SelectMany(m => m.GetCustomAttributes<ValidationAttribute>())
            .Select(a => a.ErrorMessage)
            .Where(m => m is not null)
            .ToList();

        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.Contains(m, keys));
    }
}
