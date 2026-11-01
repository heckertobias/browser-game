using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Primitives;

namespace BrowserGame.Server.Features.Localization;

/// <summary>Kürzt Kulturnamen auf die Sprache ("de-AT" → "de"). Im Invariant-Modus gibt es keine Eltern-Kulturen (006 AK-1).</summary>
internal sealed class LanguageOnlyCultureProvider(IRequestCultureProvider inner) : IRequestCultureProvider
{
    public async Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext) =>
        await inner.DetermineProviderCultureResult(httpContext) is { } result
            ? new ProviderCultureResult(LanguagesOf(result.Cultures), LanguagesOf(result.UICultures))
            : null;

    private static List<StringSegment> LanguagesOf(IList<StringSegment> names) => [.. names.Select(LanguageOf)];

    private static StringSegment LanguageOf(StringSegment name) =>
        name.IndexOf('-') is > 0 and var i ? name.Subsegment(0, i) : name;
}
