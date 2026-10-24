using BrowserGame.Data;

if (await WebDefaults.TryRunHealthProbeAsync(args) is { } exitCode)
{
    return exitCode;
}

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Services.AddGameDatabase(builder.Configuration);
builder.Services.AddAdminDatabase(builder.Configuration);
builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Das Admin migriert beide Kontexte, damit es auch ohne vorherigen Spielstart funktioniert (004 AK-5).
await app.Services.MigrateAsync<GameDbContext>(databaseProvider);
await app.Services.MigrateAsync<AdminDbContext>(databaseProvider);

app.UseSecurityHeaders();
app.UseStaticFiles();

app.MapRazorPages();
app.MapHealthChecks("/health");

await app.RunAsync();
return 0;
