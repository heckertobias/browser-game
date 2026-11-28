namespace BrowserGame.Server.Features.Game;

internal static class GameSetup
{
    public static void AddGame(this IServiceCollection services)
    {
        services.AddSingleton<MapLoader>();
        // Geladen, wenn der Host den GameLoop anlegt: vor dem ersten Tick und vor dem ersten Client. Eine ungültige Karte bricht den Start ab (018).
        services.AddSingleton(sp => sp.GetRequiredService<MapLoader>().Map);
        services.AddSingleton<GameLoop>();
        services.AddSingleton<PositionStore>();
        services.AddSingleton<ChatStore>();
        services.AddSingleton<KeyBindingStore>();
        // Vor dem GameLoop registriert: Hosted Services stoppen rückwärts, so schreiben die Stores noch dessen letzten Stand (003 AK-19, 011 AK-15).
        // Sie starten auch vorher: Der ChatStore lädt den Verlauf, bevor der GameLoop tickt und der Server Verbindungen annimmt.
        services.AddHostedService(sp => sp.GetRequiredService<PositionStore>());
        services.AddHostedService(sp => sp.GetRequiredService<ChatStore>());
        services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());
    }
}
