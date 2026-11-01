using System.Globalization;
using BrowserGame.Server.Localization;

namespace BrowserGame.Server.Tests;

/// <summary>006 T4: Die deutschen Seitentexte bleiben wie in 003 T7; die Ressourcen enthalten kein HTML.</summary>
public sealed class PageTextsTests
{
    private static readonly Dictionary<string, string> German = new()
    {
        ["Common.GameName"] = "Browser Game",
        ["Common.SignIn"] = "Anmelden",
        ["Common.Register"] = "Registrieren",
        ["Common.Email"] = "E-Mail",
        ["Index.Tagline"] = "Eine Fantasy-Welt im Browser. Kein Download – einfach loslaufen.",
        ["Index.Play"] = "Spielen",
        ["Index.SignedInAs"] = "Angemeldet als {0}",
        ["Index.SignOut"] = "Abmelden",
        ["Login.ReturnHint"] = "Melde dich an, um zu spielen.",
        ["Login.Password"] = "Passwort",
        ["Login.NoAccount"] = "Noch keinen Account? Registrieren",
        ["Login.GenericError"] = "Anmeldung fehlgeschlagen. Prüfe E-Mail und Passwort. Nach mehreren Fehlversuchen ist die Anmeldung kurz gesperrt.",
        ["Register.NameLabel"] = "Spielername (3 bis 16 Buchstaben A–Z)",
        ["Register.PasswordLabel"] = "Passwort (mindestens 10 Zeichen)",
        ["Register.ConfirmPasswordLabel"] = "Passwort wiederholen",
        ["Register.HaveAccount"] = "Schon einen Account? Anmelden",
        ["Register.PasswordMismatch"] = "Die Passwörter stimmen nicht überein.",
        ["Register.Sent"] = "Fast geschafft! Wir haben dir eine Mail mit einem Link geschickt. Über den Link wählst du deinen Spielernamen und legst ein Passwort oder einen Passkey fest. Der Link ist {0} Minuten gültig.",
        ["Register.NoMail"] = "Keine Mail bekommen? Erneut registrieren",
    };

    [Fact] // AK-10
    public void German_texts_are_unchanged_from_003()
    {
        var de = CultureInfo.GetCultureInfo("de");

        Assert.All(German, e => Assert.Equal(e.Value, Texts.ResourceManager.GetString(e.Key, de)));
    }

    [Theory] // AK-10
    [InlineData("")]
    [InlineData("de")]
    public void No_text_contains_markup(string culture)
    {
        var set = Texts.ResourceManager.GetResourceSet(CultureInfo.GetCultureInfo(culture), createIfNotExists: true, tryParents: false)!;
        var values = set.Cast<System.Collections.DictionaryEntry>().Select(e => (string)e.Value!).ToList();

        Assert.NotEmpty(values);
        Assert.DoesNotContain(values, v => v.Contains('<') || v.Contains('>'));
    }
}
