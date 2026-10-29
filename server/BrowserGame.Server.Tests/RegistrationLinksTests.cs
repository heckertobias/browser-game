using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;

namespace BrowserGame.Server.Tests;

public class RegistrationLinksTests
{
    private const string Email = "a@b.de";

    private static readonly IDataProtectionProvider Provider = DataProtectionProvider.Create("test");

    private static RegistrationLinks Links(string? lifetimeMinutes = null) =>
        new(Provider, new ConfigurationBuilder()
            .AddInMemoryCollection(lifetimeMinutes is null ? [] : [new("Registration:LinkLifetimeMinutes", lifetimeMinutes)])
            .Build());

    [Fact] // AK-2
    public void Code_round_trips_the_email()
    {
        var links = Links();

        var code = links.CreateCode(Email, TimeSpan.FromMinutes(30));

        Assert.Equal(Email, links.EmailFrom(code));
        Assert.Matches("^[A-Za-z0-9_-]+$", code);
    }

    [Theory] // AK-3
    [InlineData(null)]
    [InlineData("")]
    [InlineData("kaputt")]
    [InlineData("%%%")]
    public void Garbage_code_is_rejected(string? code) => Assert.Null(Links().EmailFrom(code));

    [Fact] // AK-3
    public void Changed_code_is_rejected()
    {
        var links = Links();
        var code = links.CreateCode(Email, TimeSpan.FromMinutes(30));

        Assert.Null(links.EmailFrom(code[..^2]));
        Assert.Null(links.EmailFrom("A" + code));
    }

    [Fact] // AK-3
    public void Expired_code_is_rejected() =>
        Assert.Null(Links().EmailFrom(Links().CreateCode(Email, TimeSpan.FromSeconds(-1))));

    [Fact] // AK-3
    public void Code_for_another_purpose_is_rejected()
    {
        var code = Provider.CreateProtector("anders").ToTimeLimitedDataProtector().Protect(Email, TimeSpan.FromHours(1));

        Assert.Null(Links().EmailFrom(code));
    }

    [Fact] // AK-3
    public void Lifetime_defaults_to_30_minutes_and_follows_the_setting()
    {
        Assert.Equal(30, Links().LifetimeMinutes);
        Assert.Equal(45, Links("45").LifetimeMinutes);
    }
}
