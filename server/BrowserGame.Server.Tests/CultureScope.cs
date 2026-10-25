using System.Globalization;

namespace BrowserGame.Server.Tests;

internal static class CultureScope
{
    /// <summary>Führt <paramref name="f"/> mit der Kultur <paramref name="name"/> aus und stellt die vorherige danach wieder her.</summary>
    public static T Run<T>(string name, Func<T> f)
    {
        var culture = CultureInfo.CurrentCulture;
        var uiCulture = CultureInfo.CurrentUICulture;
        try
        {
            var target = CultureInfo.GetCultureInfo(name);
            CultureInfo.CurrentCulture = target;
            CultureInfo.CurrentUICulture = target;
            return f();
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
            CultureInfo.CurrentUICulture = uiCulture;
        }
    }
}
