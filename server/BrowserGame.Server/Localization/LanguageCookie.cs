using Microsoft.AspNetCore.Localization;

namespace BrowserGame.Server.Localization;

/// <summary>Schreibt das Sprach-Cookie, das <c>UseRequestLocalization</c> vor <c>Accept-Language</c> liest (006 AK-4).</summary>
internal static class LanguageCookie
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public static void Append(HttpResponse response, string language)
    {
        var code = GameLanguages.Normalize(language);
        var env = response.HttpContext.RequestServices.GetRequiredService<IHostEnvironment>();
        response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(code)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow + Lifetime,
                Path = "/",
                SameSite = SameSiteMode.Lax,
                HttpOnly = false,
                Secure = response.HttpContext.Request.IsHttps || !env.IsDevelopment(),
            });
    }
}
