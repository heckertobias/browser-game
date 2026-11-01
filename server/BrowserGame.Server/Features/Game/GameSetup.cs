namespace BrowserGame.Server.Features.Game;

internal static class GameSetup
{
    public static void AddGame(this IServiceCollection services)
    {
        services.AddSingleton<GameLoop>();
        services.AddSingleton<PositionStore>();
        // Vor dem GameLoop registriert: Hosted Services stoppen rückwärts, so schreibt der Store noch dessen letzten Stand (003 AK-19).
        services.AddHostedService(sp => sp.GetRequiredService<PositionStore>());
        services.AddHostedService(sp => sp.GetRequiredService<GameLoop>());
    }
}
