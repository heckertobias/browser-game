using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

public class PasskeyOriginTests
{
    private static readonly Uri Admin = new("https://browsergame-admin.endofweb.internal");

    [Theory]
    [InlineData("https://browsergame-admin.endofweb.internal", false, true)]
    [InlineData("https://BROWSERGAME-ADMIN.endofweb.internal", false, true)]
    [InlineData("https://browsergame-admin.endofweb.internal:443", false, true)]  // Standardport
    [InlineData("https://browsergame-admin.endofweb.internal", true, false)]     // aus fremdem iframe
    [InlineData("http://browsergame-admin.endofweb.internal", false, false)]     // anderes Schema
    [InlineData("https://browsergame-admin.endofweb.internal:8443", false, false)]
    [InlineData("https://evil.endofweb.internal", false, false)]
    [InlineData("https://browsergame-admin.endofweb.internal.evil.example", false, false)]
    [InlineData("null", false, false)]
    [InlineData(null, false, false)]
    public void Origin_must_match_public_url_exactly(string? origin, bool crossOrigin, bool expected)
    {
        Assert.Equal(expected, PasskeySetup.IsExpectedOrigin(origin, crossOrigin, Admin));
    }
}
