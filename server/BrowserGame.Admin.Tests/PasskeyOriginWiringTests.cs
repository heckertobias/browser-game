using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BrowserGame.Admin.Tests;

/// <summary>Regression 0.2.1: „invalid origin“, weil die Standardprüfung den (wegen no-referrer leeren) Origin-Header vergleicht.</summary>
public sealed class PasskeyOriginWiringTests
{
    private static ValueTask<bool> Validate(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> factory, string origin, bool crossOrigin = false)
    {
        var options = factory.Services.GetRequiredService<IOptions<IdentityPasskeyOptions>>().Value;
        Assert.NotNull(options.ValidateOrigin);
        var http = new DefaultHttpContext();
        http.Request.Headers.Origin = "null"; // so schicken Browser es bei Referrer-Policy: no-referrer
        return options.ValidateOrigin(new PasskeyOriginValidationContext { HttpContext = http, Origin = origin, CrossOrigin = crossOrigin });
    }

    [Fact]
    public async Task Configured_public_url_is_the_only_valid_origin()
    {
        await using var factory = new AdminFactory();
        await using var configured = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", "https://browsergame-admin.endofweb.internal"));

        Assert.True(await Validate(configured, "https://browsergame-admin.endofweb.internal"));
        Assert.False(await Validate(configured, "https://browsergame-admin.endofweb.internal", crossOrigin: true));
        Assert.False(await Validate(configured, "https://evil.example"));
    }
}
