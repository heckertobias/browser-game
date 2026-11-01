using BrowserGame.Server.Features.Accounts;
using BrowserGame.Data;
using BrowserGame.Server.Features.Game;
using BrowserGame.Server.Features.Localization;
using BrowserGame.Server.Features.Pwa;
using BrowserGame.Server.Hosting;

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
builder.Services.AddGameLocalization();
builder.Services.AddGamePages();
builder.Services.AddHealthChecks();
builder.Services.AddGame();

var app = builder.Build();

await app.Services.MigrateAsync<GameDbContext>(databaseProvider);

app.UseSecurityHeaders();
app.UseGameLocalization();

// Erkennt Verbindungen, die ohne Close-Frame verschwinden (003 AK-20).
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
