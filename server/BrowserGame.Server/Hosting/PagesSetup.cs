using BrowserGame.Server.Features.Localization.Resources;

namespace BrowserGame.Server.Hosting;

internal static class PagesSetup
{
    public static void AddGamePages(this IServiceCollection services) =>
        services.AddRazorPages(o =>
            {
                // Razor-Seiten leben in den Feature-Ordnern (Features/<Feature>/Presentation/…).
                // Die URLs bleiben wie bisher (/Account/Login …): die Konvention entfernt <Feature> und Presentation aus der Route.
                o.RootDirectory = "/Features";
                o.Conventions.Add(new FeaturePageRouteConvention());
            })
            .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(Texts)));
}
