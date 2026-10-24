using System.Text;
using QRCoder;

namespace BrowserGame.Admin.Auth;

/// <summary>QR-Code und lesbarer Schlüssel für die Einrichtung einer Authenticator-App (RFC 6238, 6 Ziffern, 30 s).</summary>
internal static class TotpSetup
{
    private const string Issuer = "Browser Game Admin";

    public static string OtpAuthUri(string account, string key) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}?secret={key}&issuer={Uri.EscapeDataString(Issuer)}&digits=6";

    /// <summary>Inline-SVG (kein Bild-Request nötig, passt zur CSP).</summary>
    public static string QrSvg(string uri)
    {
        using var data = QRCodeGenerator.GenerateQrCode(uri, QRCodeGenerator.ECCLevel.Q);
        return new SvgQRCode(data).GetGraphic(4);
    }

    /// <summary>Schlüssel in Vierergruppen zum Abtippen.</summary>
    public static string Readable(string key)
    {
        var builder = new StringBuilder();
        for (var i = 0; i < key.Length; i++)
        {
            if (i > 0 && i % 4 == 0)
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(key[i]));
        }

        return builder.ToString();
    }
}
