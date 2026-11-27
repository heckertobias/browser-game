using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BrowserGame.Admin.Tests;

/// <summary>
/// Das Admin prüft die Passkey-Herkunft gegen <c>App:PublicUrl</c>, nicht gegen den Origin-Header: Wegen
/// <c>Referrer-Policy: no-referrer</c> schicken Browser dort <c>null</c>, und die Standardprüfung meldete „invalid origin“.
/// Regressionstest: Der Fehler ist in einer Testumgebung aufgetreten.
/// </summary>
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
        await using var configured = factory.WithWebHostBuilder(b => b.UseSetting("App:PublicUrl", "https://browsergame-admin.example.internal"));

        Assert.True(await Validate(configured, "https://browsergame-admin.example.internal"));
        Assert.False(await Validate(configured, "https://browsergame-admin.example.internal", crossOrigin: true));
        Assert.False(await Validate(configured, "https://evil.example"));
    }
}
