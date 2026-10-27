using BrowserGame.Server.Accounts;
using BrowserGame.Data;
using BrowserGame.Server.Game;
using BrowserGame.Server.Localization;
using BrowserGame.Server.Pwa;
using Microsoft.AspNetCore.Localization;

if (await WebDefaults.TryRunHealthProbeAsync(args) is { } exitCode)
{
    return exitCode;
}

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Services.AddGameDatabase(builder.Configuration);
if (databaseProvider == DatabaseProvider.Sqlite)
{
    builder.Services.AddHostedService<SqliteBackupService>();
}

builder.AddGameAccounts();
builder.AddGameEmail();
builder.Services.AddLocalization(o => o.ResourcesPath = "Resources");
builder.Services.AddRazorPages()
    .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(Texts)));
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<GameLoop>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());

var app = builder.Build();

await app.Services.MigrateAsync<GameDbContext>(databaseProvider);

app.UseSecurityHeaders();
app.UseRequestLocalization(o =>
{
    string[] cultures = [.. GameLanguages.Supported];
    o.SetDefaultCulture(GameLanguages.Default).AddSupportedCultures(cultures).AddSupportedUICultures(cultures);
    // Nur die bevorzugte Sprache zählt (006 AK-2): "fr, de" ergibt Englisch, nicht Deutsch.
    o.RequestCultureProviders =
    [
        new LanguageOnlyCultureProvider(new CookieRequestCultureProvider()),
        new LanguageOnlyCultureProvider(new AcceptLanguageHeaderRequestCultureProvider { MaximumAcceptLanguageHeaderValuesToTry = 1 }),
    ];
    // Den Rückfall auf Eltern-Kulturen übernimmt der Wrapper; so entsteht aus Nutzereingaben nie eine CultureInfo.
    o.FallBackToParentCultures = false;
    o.FallBackToParentUICultures = false;
});

// Erkennt Verbindungen, die ohne Close-Frame verschwinden (AK-4).
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10),
    KeepAliveTimeout = TimeSpan.FromSeconds(10),
});
var staticFiles = new StaticFileOptions { OnPrepareResponse = PwaCaching.Apply };
app.UseStaticFiles(staticFiles);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapRazorPages();
app.Map("/ws", GameEndpoint.HandleAsync);
app.MapHealthChecks("/health");
app.MapGet("/sw.js", ServiceWorkerScript.HandleAsync);
// Client (Vite-Build) liegt in wwwroot/play. `nonfile`: Pfade mit Dateiendung (Assets) fallen nicht auf
// index.html zurück – sonst verdeckt die Route die Static-Files-Middleware und Assets kämen als HTML.
app.MapFallbackToFile("/play", "play/index.html", staticFiles).RequireAuthorization();
app.MapFallbackToFile("/play/{*path:nonfile}", "play/index.html", staticFiles).RequireAuthorization();

await app.RunAsync();
return 0;
