using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace BrowserGame.Server.Shared.Pages;

/// <summary>
/// Razor-Seitentitel-Layout folgt den Feature-Ordnern (<c>Features/&lt;Feature&gt;/Presentation/</c>), die URLs bleiben
/// aber wie bisher (<c>/Account/Login</c> …): die Konvention schneidet <c>&lt;Feature&gt;/Presentation/</c> aus der
/// Route, die aus dem Dateipfad hinter <c>RootDirectory = /Features</c> entsteht.
/// </summary>
internal sealed class FeaturePageRouteConvention : IPageRouteModelConvention
{
    public void Apply(PageRouteModel model)
    {
        foreach (var selector in model.Selectors)
        {
            if (selector.AttributeRouteModel?.Template is not { } template) continue;
            var segments = template.Split('/');
            if (segments.Length <= 2 || segments[1] != "Presentation") continue;
            selector.AttributeRouteModel.Template = string.Join('/', segments.Skip(2));
        }
    }
}
