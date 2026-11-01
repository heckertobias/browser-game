using System.Globalization;
using System.Text.RegularExpressions;
using BrowserGame.Admin.Resources;

namespace BrowserGame.Admin.Tests;

public sealed partial class AdminViewTextsTests
{
    private static string PagesDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BrowserGame.slnx")))
                return Path.Combine(dir.FullName, "BrowserGame.Admin", "Pages");
        throw new InvalidOperationException("BrowserGame.slnx nicht gefunden.");
    }

    [Fact] // 005 AK-14
    public void Views_use_only_existing_text_keys()
    {
        var keys = Directory.EnumerateFiles(PagesDirectory(), "*.cshtml", SearchOption.AllDirectories)
            .SelectMany(file => TextKey().Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value))
            .Distinct()
            .ToList();

        Assert.NotEmpty(keys);
        foreach (var key in keys)
        {
            Assert.True(AdminTexts.ResourceManager.GetString(key, CultureInfo.InvariantCulture) is not null, key);
        }
    }

    [GeneratedRegex("\\bT\\[\"([^\"]+)\"")]
    private static partial Regex TextKey();
}
