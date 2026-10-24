using BrowserGame.Server.Accounts;
using BrowserGame.Data;
using BrowserGame.Server.Game;

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
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<GameLoop>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());

var app = builder.Build();

await app.Services.MigrateAsync<GameDbContext>(databaseProvider);

app.UseSecurityHeaders();

// Erkennt Verbindungen, die ohne Close-Frame verschwinden (AK-4).
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10),
    KeepAliveTimeout = TimeSpan.FromSeconds(10),
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapRazorPages();
app.Map("/ws", GameEndpoint.HandleAsync);
app.MapHealthChecks("/health");
// Client (Vite-Build) liegt in wwwroot/play. `nonfile`: Pfade mit Dateiendung (Assets) fallen nicht auf
// index.html zurück – sonst verdeckt die Route die Static-Files-Middleware und Assets kämen als HTML.
app.MapFallbackToFile("/play", "play/index.html");
app.MapFallbackToFile("/play/{*path:nonfile}", "play/index.html");

await app.RunAsync();
return 0;
