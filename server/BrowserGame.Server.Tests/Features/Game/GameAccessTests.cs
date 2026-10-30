using BrowserGame.Server.Features.Game;

namespace BrowserGame.Server.Tests;

public sealed class GameAccessTests
{
    private static readonly Uri PublicUrl = new("https://game.test");
    private static readonly Uri RequestOrigin = new("http://localhost:5080");

    [Theory]
    [InlineData("https://game.test", false, true)]
    [InlineData("https://evil.example", false, false)]
    [InlineData("http://game.test", false, false)]
    [InlineData(null, false, false)]
    [InlineData("null", false, false)]
    [InlineData("http://localhost:5173", false, false)]
    [InlineData("http://localhost:5173", true, true)]
    [InlineData("http://localhost:5080", true, false)]
    public void Origin_is_checked_against_public_url(string? origin, bool isDevelopment, bool expected) =>
        Assert.Equal(expected, GameAccess.IsAllowedOrigin(origin, PublicUrl, isDevelopment, RequestOrigin));

    [Theory]
    [InlineData("http://localhost:5080", true, true)]
    [InlineData("http://localhost:5173", true, true)]
    [InlineData("https://evil.example", true, false)]
    [InlineData("http://localhost:5080", false, false)]
    public void Origin_without_public_url_falls_back_to_request_in_development(string origin, bool isDevelopment, bool expected) =>
        Assert.Equal(expected, GameAccess.IsAllowedOrigin(origin, null, isDevelopment, RequestOrigin));
}
