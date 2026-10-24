using System.Text.Encodings.Web;

namespace BrowserGame.Data;

/// <summary>Schlichtes HTML aus einer Text-Mail: alles escaped, Zeilenumbrüche erhalten, der Link klickbar.</summary>
public static class MailHtml
{
    public static string FromText(string text, string? link = null)
    {
        var encoded = HtmlEncoder.Default.Encode(text).Replace("&#xA;", "<br>", StringComparison.Ordinal);
        if (string.IsNullOrEmpty(link))
        {
            return encoded;
        }

        var encodedLink = HtmlEncoder.Default.Encode(link);
        return encoded.Replace(encodedLink, $"<a href=\"{encodedLink}\">{encodedLink}</a>", StringComparison.Ordinal);
    }
}
