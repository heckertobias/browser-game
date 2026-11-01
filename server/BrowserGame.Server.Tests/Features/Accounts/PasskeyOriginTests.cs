using BrowserGame.Data;

namespace BrowserGame.Server.Tests;

public class PasskeyOriginTests
{
    private static readonly Uri Admin = new("https://browsergame-admin.example.internal");

    [Theory]
    [InlineData("https://browsergame-admin.example.internal", false, true)]
    [InlineData("https://BROWSERGAME-ADMIN.example.internal", false, true)]
    [InlineData("https://browsergame-admin.example.internal:443", false, true)]  // Standardport
    [InlineData("https://browsergame-admin.example.internal", true, false)]     // aus fremdem iframe
    [InlineData("http://browsergame-admin.example.internal", false, false)]     // anderes Schema
    [InlineData("https://browsergame-admin.example.internal:8443", false, false)]
    [InlineData("https://evil.example.internal", false, false)]
    [InlineData("https://browsergame-admin.example.internal.evil.example", false, false)]
    [InlineData("null", false, false)]
    [InlineData(null, false, false)]
    public void Origin_must_match_public_url_exactly(string? origin, bool crossOrigin, bool expected)
    {
        Assert.Equal(expected, PasskeySetup.IsExpectedOrigin(origin, crossOrigin, Admin));
    }
}
