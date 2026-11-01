using BrowserGame.Server.Features.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;

namespace BrowserGame.Server.Tests;

public class AccountLinksTests
{
    private sealed class Environment(string name) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static AccountLinks LinksFor(string environment, string? publicUrl)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(publicUrl is null ? [] : [new("App:PublicUrl", publicUrl)])
            .Build();
        return new AccountLinks(configuration, new HttpContextAccessor(), new Environment(environment));
    }

    [Theory] // AK-3
    [InlineData("abc")]
    [InlineData("a+b/c=d")]
    [InlineData("CfDJ8Ä+/==")]
    public void Token_roundtrips(string token)
    {
        var encoded = AccountLinks.EncodeToken(token);

        Assert.DoesNotContain('+', encoded);
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('=', encoded);
        Assert.Equal(token, AccountLinks.DecodeToken(encoded));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("!!!")]
    public void Garbage_token_decodes_to_null(string? encoded) => Assert.Null(AccountLinks.DecodeToken(encoded));

    [Theory] // AK-1
    [InlineData(null)]
    [InlineData("")]
    public void Missing_return_url_leads_to_game(string? returnUrl) =>
        Assert.Equal("/play", AccountLinks.SafeReturnUrl(returnUrl, _ => true));

    [Fact] // AK-1
    public void Local_return_url_is_kept() =>
        Assert.Equal("/play/irgendwas", AccountLinks.SafeReturnUrl("/play/irgendwas", _ => true));

    [Fact]
    public void Non_local_return_url_leads_to_game() =>
        Assert.Equal("/play", AccountLinks.SafeReturnUrl("/anderswo", _ => false));

    [Fact]
    public void Absolute_uses_configured_public_url_without_double_slash() =>
        Assert.Equal("https://game.test/Account/Login", LinksFor("Production", "https://game.test/").Absolute("/Account/Login"));

    [Fact]
    public void Absolute_without_public_url_fails_outside_development() =>
        Assert.Throws<InvalidOperationException>(() => LinksFor("Production", null).Absolute("/Account/Login"));
}
