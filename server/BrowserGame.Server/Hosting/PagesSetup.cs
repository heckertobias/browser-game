using BrowserGame.Server.Features.Localization.Resources;

namespace BrowserGame.Server.Hosting;

/// <summary>Razor Pages des Spielservers: Seiten aus den Feature-Ordnern, Validierungstexte aus <see cref="Texts"/>.</summary>
internal static class PagesSetup
{
    public static void AddGamePages(this IServiceCollection services) =>
        services.AddRazorPages(o =>
            {
                // Razor-Seiten leben in den Feature-Ordnern (Features/<Feature>/Presentation/…); die Konvention entfernt
                // <Feature> und Presentation aus der Route, damit die URLs kurz bleiben (/Account/Login …).
                o.RootDirectory = "/Features";
                o.Conventions.Add(new FeaturePageRouteConvention());
            })
            .AddDataAnnotationsLocalization(o => o.DataAnnotationLocalizerProvider = (_, f) => f.Create(typeof(Texts)));
}
