using System.Text.RegularExpressions;
using BrowserGame.Admin.Auth;
using BrowserGame.Data;

namespace BrowserGame.Admin.Tests;

/// <summary>Durchläuft Einladung und Anmeldung über die echten Seiten, so wie ein Admin im Browser.</summary>
public static partial class AdminTestFlows
{
    public const string Password = "ein-langes-admin-passwort";

    public sealed record CreatedAdmin(FormClient Client, string Email, string TotpKey, IReadOnlyList<string> RecoveryCodes);

    /// <summary>Token aus dem Einrichtungslink im Log (erster Start, 004 AK-1).</summary>
    public static string SetupTokenFromLog(AdminFactory factory)
    {
        _ = factory.Server; // Start erzwingen
        var message = factory.Logs.Messages.LastOrDefault(m => m.Contains("Einrichtungslink", StringComparison.Ordinal))
            ?? throw new InvalidOperationException("Kein Einrichtungslink im Log.");
        return TokenFromLink(message);
    }

    public static async Task<string> InviteTokenAsync(AdminFactory factory, string createdBy = "Test")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var (_, link) = await scope.ServiceProvider.GetRequiredService<AdminInvites>().CreateAsync(createdBy, email: null);
        return TokenFromLink(link);
    }

    /// <summary>Löst eine Einladung mit Passwort + Authenticator ein; der Client ist danach angemeldet.</summary>
    public static async Task<CreatedAdmin> RedeemWithPasswordAsync(AdminFactory factory, string token, string name, string email)
    {
        var client = factory.CreateFormClient();
        var invite = $"/Invite?token={token}";

        var (_, chooseMethod) = await client.PostAsync(invite, "/Invite?handler=Details",
            new Dictionary<string, string> { ["Token"] = token, ["Name"] = name, ["Email"] = email });
        var (_, totpPage) = await client.PostWithTokenAsync("/Invite?handler=Password",
            new Dictionary<string, string> { ["Token"] = token, ["Password"] = Password, ["ConfirmPassword"] = Password },
            FormClient.AntiforgeryToken(chooseMethod));
        var key = TotpKey(totpPage);
        var (_, done) = await client.PostWithTokenAsync("/Invite?handler=Totp",
            new Dictionary<string, string> { ["Token"] = token, ["Code"] = Totp.Code(key) },
            FormClient.AntiforgeryToken(totpPage));

        var codes = RecoveryCodePattern().Matches(done[(done.IndexOf("data-testid=\"recovery-codes\"", StringComparison.Ordinal) is var i and >= 0 ? i : 0)..])
            .Select(m => m.Groups[1].Value).ToList();
        if (codes.Count == 0)
        {
            throw new InvalidOperationException("Einrichtung nicht abgeschlossen:\n" + done);
        }

        return new CreatedAdmin(client, email, key, codes);
    }

    /// <summary>Passwort-Schritt der Anmeldung; liefert die Antwort (Redirect auf den 2FA-Schritt bei Erfolg).</summary>
    public static Task<(HttpResponseMessage Response, string Html)> LoginPasswordAsync(FormClient client, string email, string password = Password) =>
        client.PostAsync("/Login", "/Login?handler=Password", new Dictionary<string, string> { ["Email"] = email, ["Password"] = password });

    public static Task<(HttpResponseMessage Response, string Html)> LoginCodeAsync(FormClient client, string code, bool recovery = false) =>
        client.PostAsync("/LoginTwoFactor", "/LoginTwoFactor",
            new Dictionary<string, string> { ["Code"] = code, ["IsRecoveryCode"] = recovery ? "true" : "false" });

    public static async Task<FormClient> LoginAsync(AdminFactory factory, CreatedAdmin admin)
    {
        var client = factory.CreateFormClient();
        await LoginPasswordAsync(client, admin.Email);
        await LoginCodeAsync(client, Totp.Code(admin.TotpKey));
        return client;
    }

    private static string TokenFromLink(string text) => TokenPattern().Match(text).Groups[1].Value;

    private static string TotpKey(string html) =>
        TotpKeyPattern().Match(html) is { Success: true } match
            ? match.Groups[1].Value.Replace(" ", "", StringComparison.Ordinal)
            : throw new InvalidOperationException("Kein Authenticator-Schlüssel:\n" + html);

    [GeneratedRegex(@"token=([A-Za-z0-9_\-]+)")]
    private static partial Regex TokenPattern();

    [GeneratedRegex("<p class=\"font-mono text-sm break-all\">([a-z0-9 ]+)</p>")]
    private static partial Regex TotpKeyPattern();

    [GeneratedRegex(@"<li>([A-Za-z0-9\-]+)</li>")]
    private static partial Regex RecoveryCodePattern();
}
