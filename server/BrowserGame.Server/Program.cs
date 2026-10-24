using BrowserGame.Server.Game;

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

app.Run();
