using BrowserGame.Server.Game;

// Docker-HEALTHCHECK: Das chiseled Image hat kein curl, deshalb prüft der Server sich selbst.
if (args is ["--healthcheck", ..])
{
    var port = Environment.GetEnvironmentVariable("ASPNETCORE_HTTP_PORTS")?.Split(';')[0] ?? "8080";
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
    try
    {
        using var response = await http.GetAsync(new Uri($"http://localhost:{port}/health"));
        return response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        return 1;
    }
    catch (TaskCanceledException)
    {
        return 1;
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<GameLoop>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());

var app = builder.Build();

app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers.XContentTypeOptions = "nosniff";
    headers["Referrer-Policy"] = "no-referrer";
    headers.ContentSecurityPolicy =
        "default-src 'self'; connect-src 'self'; img-src 'self' data:; object-src 'none'; base-uri 'self'; frame-ancestors 'none'";
    await next();
});

// Erkennt Verbindungen, die ohne Close-Frame verschwinden (AK-4).
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(10),
    KeepAliveTimeout = TimeSpan.FromSeconds(10),
});
app.UseStaticFiles();

app.MapRazorPages();
app.Map("/ws", GameEndpoint.HandleAsync);
app.MapHealthChecks("/health");
// Client (Vite-Build) liegt in wwwroot/play.
app.MapFallbackToFile("/play/{**path}", "play/index.html");

await app.RunAsync();
return 0;
