using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BrowserGame.Server.Hosting;

/// <summary>
/// Razor-Seiten liegen in den Feature-Ordnern (<c>Features/&lt;Feature&gt;/Presentation/</c>), die Ordner gehören aber
/// nicht in die URL (<c>/Account/Login</c> statt <c>/Accounts/Presentation/Account/Login</c>): Die Konvention schneidet
/// <c>&lt;Feature&gt;/Presentation/</c> aus der Route, die aus dem Dateipfad hinter <c>RootDirectory = /Features</c>
/// entsteht. So bleiben URLs stabil, wenn Seiten zwischen Features wandern.
/// </summary>
internal sealed class FeaturePageRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        foreach (var selector in model.Selectors)
        {
            if (selector.AttributeRouteModel?.Template is not { } template)
            {
                continue;
            }

            var segments = template.Split('/');
            if (segments.Length <= 2 || segments[1] != "Presentation")
            {
                continue;
            }

            selector.AttributeRouteModel.Template = string.Join('/', segments.Skip(2));
        }
    }
}
