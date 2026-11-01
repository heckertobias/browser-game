using Microsoft.AspNetCore.Localization;

namespace BrowserGame.Server.Features.Localization;

internal static class LocalizationSetup
{
    public static void AddGameLocalization(this IServiceCollection services) =>
        services.AddLocalization(o => o.ResourcesPath = "");

    public static void UseGameLocalization(this IApplicationBuilder app) =>
        app.UseRequestLocalization(o =>
        {
            string[] cultures = [.. GameLanguages.Supported];
            o.SetDefaultCulture(GameLanguages.Default).AddSupportedCultures(cultures).AddSupportedUICultures(cultures);
            // Nur die bevorzugte Sprache zählt (006 AK-2): "fr, de" ergibt Englisch, nicht Deutsch.
            o.RequestCultureProviders =
            [
                new LanguageOnlyCultureProvider(new CookieRequestCultureProvider()),
                new LanguageOnlyCultureProvider(new AcceptLanguageHeaderRequestCultureProvider { MaximumAcceptLanguageHeaderValuesToTry = 1 }),
            ];
            // Den Rückfall auf Eltern-Kulturen übernimmt der Wrapper; so entsteht aus Nutzereingaben nie eine CultureInfo.
            o.FallBackToParentCultures = false;
            o.FallBackToParentUICultures = false;
        });
}
