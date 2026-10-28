using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;

namespace BrowserGame.Server.Tests;

/// <summary>
/// Startet den echten Server im Speicher, mit eigener Datenbank pro Factory und Platzhalter-wwwroot (index.html, offline.html, test-asset.js)
/// (dazu das echte Manifest und die echten Icons aus dem Server-Projekt).
/// Datenbank: SQLite-Temp-Datei (Standard) oder PostgreSQL, wenn <c>TEST_DB_PROVIDER=Postgres</c> und
/// <c>TEST_POSTGRES</c> (Verbindung ohne Datenbanknamen) gesetzt sind – so laufen dieselben Tests gegen beide (ADR 0004).
/// </summary>
public sealed class GameServerFactory : WebApplicationFactory<Program>
{
    private readonly string _root = Directory.CreateTempSubdirectory("browsergame-test").FullName;

    public GameServerFactory()
    {
        var play = Path.Combine(WebRoot, "play");
        Directory.CreateDirectory(Path.Combine(play, "assets"));
        // Fallback, falls kein Client-Build vorliegt; liegt einer in wwwroot/play, wird ggf. der echte ausgeliefert.
        File.WriteAllText(Path.Combine(play, "index.html"), "<!doctype html><div id=\"root\"></div>");
        File.WriteAllText(Path.Combine(play, "assets", "test-asset.js"), "// test-asset");
        File.WriteAllText(Path.Combine(play, "offline.html"),
            "<!doctype html><title>offline</title><script type=\"module\" src=\"/play/assets/test-asset.js\"></script>");
        // 007: Manifest und Icons sind eingecheckt – echte Dateien aus dem Server-Projekt verwenden.
        var source = ServerWebRoot();
        File.Copy(Path.Combine(source, "manifest.webmanifest"), Path.Combine(WebRoot, "manifest.webmanifest"));
        var icons = Directory.CreateDirectory(Path.Combine(WebRoot, "icons")).FullName;
        foreach (var icon in Directory.EnumerateFiles(Path.Combine(source, "icons"), "*.png"))
            File.Copy(icon, Path.Combine(icons, Path.GetFileName(icon)));
    }

    private static string ServerWebRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "BrowserGame.slnx")))
                return Path.Combine(dir.FullName, "BrowserGame.Server", "wwwroot");
        throw new InvalidOperationException("BrowserGame.slnx nicht gefunden.");
    }

    public static string Provider => TestDatabase.Provider;

    private string WebRoot => Path.Combine(_root, "wwwroot");

    /// <summary>Verbindungszeichenfolge dieser Factory; bleibt gleich, damit ein zweiter Server dieselbe DB nutzen kann.</summary>
    public string ConnectionString { get; } = TestDatabase.NewConnectionString("bg-test");

    /// <summary>Nachlauf getrennter Figuren in Sekunden (003 AK-20); Tests, die darin reconnecten, setzen mehr.</summary>
    public string LingerSeconds { get; init; } = "0.5";

    /// <summary>Statt echter Mails: alle Mails, die der Server verschicken würde.</summary>
    public CapturingEmailSender Mails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseWebRoot(WebRoot);
        builder.UseSetting("Database:Provider", Provider);
        builder.UseSetting("ConnectionStrings:Game", ConnectionString);
        // Im Testserver kommen alle Anfragen von derselben „IP“ – Limit hoch, damit Tests sich nicht gegenseitig bremsen.
        builder.UseSetting("RateLimiting:AuthRequestsPerMinute", "1000");
        // Verkürzter Nachlauf (003 AK-20), damit Tests nicht 20 s warten.
        builder.UseSetting("Game:LingerSeconds", LingerSeconds);
        builder.ConfigureTestServices(services => services.AddSingleton<GameEmailSender>(Mails));
    }

    /// <summary>Formular-Client mit eigenem Cookie-Speicher, ohne automatische Weiterleitungen.</summary>
    public FormClient CreateFormClient() => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));

    protected override void ConfigureClient(HttpClient client)
    {
        base.ConfigureClient(client);
        client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("de");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Directory.Delete(_root, recursive: true);
        TestDatabase.Drop(ConnectionString);
    }
}
