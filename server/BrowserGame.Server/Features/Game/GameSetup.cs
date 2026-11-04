namespace BrowserGame.Server.Features.Game;

internal static class GameSetup
{
    public static void AddGame(this IServiceCollection services)
    {
        services.AddSingleton<GameLoop>();
        services.AddSingleton<PositionStore>();
        services.AddSingleton<ChatStore>();
        // Vor dem GameLoop registriert: Hosted Services stoppen rückwärts, so schreiben die Stores noch dessen letzten Stand (003 AK-19, 011 AK-15).
        // Sie starten auch vorher: Der ChatStore lädt den Verlauf, bevor der GameLoop tickt und der Server Verbindungen annimmt.
        services.AddHostedService(sp => sp.GetRequiredService<PositionStore>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatStore>());
        services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());
    }
}
