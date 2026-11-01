using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Infrastructure;

namespace BrowserGame.Server.Tests;

/// <summary>Ordnungsregeln für Razor-Seiten (docs/project-structure.md, ADR 0013): Seiten liegen unter <c>Features/&lt;Feature&gt;/Presentation/</c>, URLs bleiben eindeutig.</summary>
public sealed class PageStructureTests(GameServerFactory factory) : IClassFixture<GameServerFactory>
{
    private List<PageActionDescriptor> Pages()
    {
        using var scope = factory.Services.CreateScope();
        return [.. scope.ServiceProvider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<PageActionDescriptor>()];
    }

    [Fact]
    public void Pages_are_found_and_have_unique_routes()
    {
        var pages = Pages();
        Assert.NotEmpty(pages);

        var routes = pages.Select(p => p.AttributeRouteInfo?.Template).ToList();
        Assert.DoesNotContain(null, routes);
        Assert.Equal(routes.Count, routes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Pages_live_in_a_feature_presentation_folder()
    {
        foreach (var page in Pages().Select(p => p.RelativePath).Where(p => p != "/Features/Index.cshtml"))
        {
            var parts = page.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(parts is ["Features", _, "Presentation", _, ..], $"{page} gehört nach Features/<Feature>/Presentation/.");
            Assert.NotEqual("Index.cshtml", parts[^1]);
        }
    }
}
