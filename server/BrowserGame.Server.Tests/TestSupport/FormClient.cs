using System.Net;
using System.Text.RegularExpressions;

namespace BrowserGame.Server.Tests;

/// <summary>Bedient Razor-Formulare wie ein Browser: Seite laden, Antiforgery-Token übernehmen, absenden.</summary>
public sealed partial class FormClient(HttpClient http) : IDisposable
{
    public HttpClient Http => http;

    public async Task<(HttpResponseMessage Response, string Html)> GetAsync(string url)
    {
        var response = await http.GetAsync(new Uri(url, UriKind.Relative));
        return (response, await response.Content.ReadAsStringAsync());
    }

    /// <summary>Lädt <paramref name="pageUrl"/>, nimmt das Antiforgery-Token und schickt das Formular an <paramref name="postUrl"/>.</summary>
    public async Task<(HttpResponseMessage Response, string Html)> PostAsync(string pageUrl, string postUrl, IDictionary<string, string> fields)
    {
        var (_, page) = await GetAsync(pageUrl);
        return await PostWithTokenAsync(postUrl, fields, AntiforgeryToken(page));
    }

    public async Task<(HttpResponseMessage Response, string Html)> PostWithTokenAsync(string postUrl, IDictionary<string, string> fields, string token)
    {
        var form = new Dictionary<string, string>(fields) { ["__RequestVerificationToken"] = token };
        using var content = new FormUrlEncodedContent(form);
        var response = await http.PostAsync(new Uri(postUrl, UriKind.Relative), content);
        return (response, await response.Content.ReadAsStringAsync());
    }

    public static string AntiforgeryToken(string html) =>
        AntiforgeryPattern().Match(html) is { Success: true } match
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException("Kein Antiforgery-Token im HTML.");

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();

    public void Dispose() => http.Dispose();
}
