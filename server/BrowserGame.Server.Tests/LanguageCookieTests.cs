using BrowserGame.Server.Localization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Hosting;
using Microsoft.Net.Http.Headers;

namespace BrowserGame.Server.Tests;

public sealed class LanguageCookieTests
{
    private sealed class Env(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    }

    private static SetCookieHeaderValue Append(string? language, string environment = "Production", bool https = true)
    {
        var services = new ServiceCollection().AddSingleton<IHostEnvironment>(new Env(environment)).BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Scheme = https ? "https" : "http";

        LanguageCookie.Append(context.Response, language!);

        return SetCookieHeaderValue.Parse(context.Response.Headers.SetCookie.Single());
    }

    [Fact] // AK-4
    public void Cookie_carries_the_culture_in_the_standard_format()
    {
        var cookie = Append("de");

        Assert.Equal(CookieRequestCultureProvider.DefaultCookieName, cookie.Name.Value);
        Assert.Equal("c=de|uic=de", Uri.UnescapeDataString(cookie.Value.Value!));
    }

    [Fact] // AK-4
    public void Cookie_lasts_one_year_and_is_readable_by_script_on_the_whole_site()
    {
        var cookie = Append("de");

        Assert.InRange(cookie.Expires!.Value - DateTimeOffset.UtcNow, TimeSpan.FromDays(364), TimeSpan.FromDays(366));
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
        Assert.False(cookie.HttpOnly);
        Assert.Equal("/", cookie.Path.Value);
    }

    [Theory] // AK-4
    [InlineData("Production", false, true)]
    [InlineData("Development", false, false)]
    [InlineData("Development", true, true)]
    public void Secure_follows_environment_and_scheme(string environment, bool https, bool secure) =>
        Assert.Equal(secure, Append("de", environment, https).Secure);

    [Theory] // AK-4
    [InlineData("fr")]
    [InlineData(null)]
    public void Unsupported_language_falls_back_to_english(string? language) =>
        Assert.Equal("c=en|uic=en", Uri.UnescapeDataString(Append(language).Value.Value!));
}
