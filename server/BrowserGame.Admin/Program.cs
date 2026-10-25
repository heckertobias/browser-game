using BrowserGame.Admin.Auth;
using BrowserGame.Data;

if (await WebDefaults.TryRunHealthProbeAsync(args) is { } exitCode)
{
    return exitCode;
}

var builder = WebApplication.CreateBuilder(args);

var databaseProvider = builder.Services.AddGameDatabase(builder.Configuration);
builder.Services.AddAdminDatabase(builder.Configuration);
builder.AddAdminAuth();
builder.Services.AddRazorPages(options =>
{
    // Alles nur angemeldet (004 AK-3) – außer Anmeldung und Einladung.
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Login");
    options.Conventions.AllowAnonymousToPage("/LoginTwoFactor");
    options.Conventions.AllowAnonymousToPage("/Invite");
});
builder.Services.AddHealthChecks();

var app = builder.Build();

// Das Admin migriert beide Kontexte, damit es auch ohne vorherigen Spielstart funktioniert (004 AK-5).
await app.Services.MigrateAsync<GameDbContext>(databaseProvider);
await app.Services.MigrateAsync<AdminDbContext>(databaseProvider);
await using (var scope = app.Services.CreateAsyncScope())
{
    var services = scope.ServiceProvider;
    await services.GetRequiredService<AdminInvites>().EnsureSetupLinkAsync(services.GetRequiredService<AdminCredentials>());
}

app.UseSecurityHeaders();
// Bis 005 fest Deutsch (006): eine Sprache, kein Provider – Browser und Cookie ändern nichts.
app.UseRequestLocalization(o =>
{
    o.SetDefaultCulture("de").AddSupportedCultures("de").AddSupportedUICultures("de");
    o.RequestCultureProviders.Clear();
});
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapRazorPages();
app.MapHealthChecks("/health");

await app.RunAsync();
return 0;
