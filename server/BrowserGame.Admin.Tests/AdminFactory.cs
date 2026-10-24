using BrowserGame.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using BrowserGame.Server.Tests;
using Microsoft.Extensions.Logging;

namespace BrowserGame.Admin.Tests;

/// <summary>
/// Startet das Admin im Speicher mit eigener Datenbank (SQLite-Temp-Datei oder PostgreSQL über
/// <c>TEST_DB_PROVIDER=Postgres</c> + <c>TEST_POSTGRES</c>, wie die Spiel-Tests).
/// </summary>
public sealed class AdminFactory : WebApplicationFactory<Program>
{
    /// <summary>Im Testserver kommen alle Anfragen von derselben „IP“ – Standard hoch, damit nur der Rate-Limit-Test limitiert.</summary>
    public int AuthRequestsPerMinute { get; init; } = 1000;

    /// <summary>Alle Log-Meldungen des Admin-Dienstes (z. B. der Einrichtungslink).</summary>
    public LogCapture Logs { get; } = new();

    public static string Provider => TestDatabase.Provider;

    public string ConnectionString { get; } = TestDatabase.NewConnectionString("bg-admin-test");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Database:Provider", Provider);
        builder.UseSetting("ConnectionStrings:Game", ConnectionString);
        builder.UseSetting("RateLimiting:AuthRequestsPerMinute", AuthRequestsPerMinute.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
    }

    /// <summary>Formular-Client mit eigenem Cookie-Speicher, ohne automatische Weiterleitungen.</summary>
    public FormClient CreateFormClient() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TestDatabase.Drop(ConnectionString);
    }
}
