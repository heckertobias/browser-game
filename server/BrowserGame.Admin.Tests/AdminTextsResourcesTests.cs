using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;
using BrowserGame.Admin.Resources;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Tests;

public sealed class AdminTextsResourcesTests
{
    private static Dictionary<string, string> Neutral() =>
        AdminTexts.ResourceManager.GetResourceSet(CultureInfo.InvariantCulture, createIfNotExists: true, tryParents: false) is { } set
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
    public void Validation_messages_are_resource_keys()
    {
        var keys = Neutral().Keys;
        var messages = typeof(AdminTexts).Assembly.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(p => p.GetCustomAttributes<ValidationAttribute>())
            .Select(a => a.ErrorMessage)
            .Where(m => m is not null)
            .ToList();

        Assert.NotEmpty(messages);
        Assert.All(messages, m => Assert.Contains(m, keys));
    }

    [Fact] // 005 AK-14
    public async Task Localizer_finds_every_text()
    {
        await using var factory = new AdminFactory();
        var localizer = factory.Services.GetRequiredService<IStringLocalizer<AdminTexts>>();

        foreach (var (key, value) in Neutral())
        {
            Assert.False(localizer[key].ResourceNotFound, key);
            Assert.Equal(value, localizer[key].Value);
        }
    }
}
