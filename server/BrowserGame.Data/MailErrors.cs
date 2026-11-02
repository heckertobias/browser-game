using System.Globalization;
using System.Resources;

namespace BrowserGame.Data;

/// <summary>Fehlertexte des Mailversands (<see cref="MailResult.Error"/>) in der Sprache der Anfrage; neutral = Englisch (ADR 0008).</summary>
internal static class MailErrors
{
    internal static ResourceManager ResourceManager { get; } = new("BrowserGame.Data.Resources.MailErrors", typeof(MailErrors).Assembly);

    public static string NotConfigured => Get(nameof(NotConfigured));

    public static string AuthenticationFailed => Get(nameof(AuthenticationFailed));

    public static string CertificateNameSeparator => Get(nameof(CertificateNameSeparator));

    public static string CertificateNameMismatch(IEnumerable<string> names, string host) =>
        Get(nameof(CertificateNameMismatch), string.Join(CertificateNameSeparator, names), host);

    public static string SslHandshakeFailed => Get(nameof(SslHandshakeFailed));

    public static string Unreachable => Get(nameof(Unreachable));

    public static string Rejected(string reason) => Get(nameof(Rejected), reason);

    public static string UnexpectedResponse => Get(nameof(UnexpectedResponse));

    public static string InvalidAddress => Get(nameof(InvalidAddress));

    public static string TimedOut => Get(nameof(TimedOut));

    public static string ConnectionLost => Get(nameof(ConnectionLost));

    public static string InvalidSettings => Get(nameof(InvalidSettings));

    private static string Get(string key, params object[] args)
    {
        var text = ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? key;
        return args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args);
    }
}
