using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace BrowserGame.Server.Tests;

/// <summary>Durchläuft Registrierung und Anmeldung über die echten Seiten, so wie ein Spieler im Browser.</summary>
public static partial class AccountTestFlows
{
    public const string Password = "ein-langes-passwort";

    public sealed record CreatedAccount(string Email, string Name);

    public static string UniqueEmail() => $"{Guid.NewGuid():N}@example.org";

    /// <summary>Gültiger Name (nur Buchstaben, 13 Zeichen): hexadezimale Ziffern werden auf g–p abgebildet.</summary>
    public static string UniqueName() =>
        "T" + new string(Guid.NewGuid().ToString("N")[..12].Select(c => char.IsDigit(c) ? (char)('g' + (c - '0')) : c).ToArray());

    public const string CompleteRegistrationPath = "/Account/CompleteRegistration";

    /// <summary>Schritt 1: nur die E-Mail-Adresse.</summary>
    public static Task<(HttpResponseMessage Response, string Html)> RequestRegistrationAsync(FormClient client, string email) =>
        client.PostAsync("/Account/Register", "/Account/Register", new Dictionary<string, string> { ["Email"] = email });

    /// <summary>Der neueste Registrierungslink (Pfad und Query) an <paramref name="email"/>.</summary>
    public static string RegistrationLink(GameServerFactory factory, string email) =>
        factory.Mails.To(email).Select(m => LinkFromMail(m.Text)).Last(l => l.StartsWith(CompleteRegistrationPath + "?", StringComparison.Ordinal));

    /// <summary>Schritt 2, Name wählen; bei gültigem Namen enthält die Antwort Passwort- und Passkey-Formular.</summary>
    public static Task<(HttpResponseMessage Response, string Html)> ChooseNameAsync(FormClient client, string link, string name) =>
        client.PostAsync(link, link, new Dictionary<string, string> { ["Name"] = name });

    /// <summary>Schritt 2 mit Passwort (der Server prüft den Namen erneut).</summary>
    public static Task<(HttpResponseMessage Response, string Html)> CompleteWithPasswordAsync(
        FormClient client, string link, string name, string password = Password, string? confirm = null) =>
        client.PostAsync(link, link, new Dictionary<string, string>
        {
            ["Step"] = "password",
            ["Name"] = name,
            ["Password"] = password,
            ["ConfirmPassword"] = confirm ?? password,
        });

    /// <summary>Schritt 2 mit Passkey: Name wählen, dann den Passkey des Authenticators anlegen.</summary>
    public static async Task<(HttpResponseMessage Response, string Html)> CompleteWithPasskeyAsync(
        FormClient client, string link, string name, SoftwareAuthenticator authenticator)
    {
        var (_, page) = await ChooseNameAsync(client, link, name);
        return await client.PostWithTokenAsync(link, new Dictionary<string, string>
        {
            ["Step"] = "passkey",
            ["Name"] = name,
            ["Credential"] = authenticator.Create(PasskeyOptions(page)),
        }, FormClient.AntiforgeryToken(page));
    }

    /// <summary>Beide Schritte mit Passwort; liefert die letzte Antwort (Erfolg: Redirect auf /play, angemeldet).</summary>
    public static async Task<(HttpResponseMessage Response, string Html)> RegisterAsync(
        GameServerFactory factory, FormClient client, string email, string name, string password = Password, string? confirm = null)
    {
        await RequestRegistrationAsync(client, email);
        return await CompleteWithPasswordAsync(client, RegistrationLink(factory, email), name, password, confirm);
    }

    public static Task<(HttpResponseMessage Response, string Html)> LoginAsync(
        FormClient client, string email, string password = Password, string? returnUrl = null) =>
        client.PostAsync("/Account/Login", "/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? "",
        });

    public static Task<(HttpResponseMessage Response, string Html)> ForgotPasswordAsync(FormClient client, string email) =>
        client.PostAsync("/Account/ForgotPassword", "/Account/ForgotPassword", new Dictionary<string, string> { ["Email"] = email });

    /// <summary>Löst den Link aus der Reset-Mail ein: übernimmt <c>userId</c> und <c>code</c> aus dem Link.</summary>
    public static Task<(HttpResponseMessage Response, string Html)> ResetPasswordAsync(
        FormClient client, string link, string password, string? confirm = null)
    {
        var query = System.Web.HttpUtility.ParseQueryString(link[(link.IndexOf('?', StringComparison.Ordinal) + 1)..]);
        return client.PostAsync(link, "/Account/ResetPassword", new Dictionary<string, string>
        {
            ["UserId"] = query["userId"] ?? "",
            ["Code"] = query["code"] ?? "",
            ["Password"] = password,
            ["ConfirmPassword"] = confirm ?? password,
        });
    }

    /// <summary>Legt einen Account mit bestätigter E-Mail-Adresse direkt über Identity an.</summary>
    public static Task<CreatedAccount> CreateConfirmedAccountAsync(GameServerFactory factory) =>
        CreateConfirmedAccountAsync(factory, language: null);

    public static async Task<CreatedAccount> CreateConfirmedAccountAsync(GameServerFactory factory, string? language)
    {
        var account = new CreatedAccount(UniqueEmail(), UniqueName());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var user = new Account { UserName = account.Name, Email = account.Email, EmailConfirmed = true };
        if (language is not null)
        {
            user.Language = language;
        }

        var result = await users.CreateAsync(user, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    /// <summary>Die WebAuthn-Optionen (JSON) aus dem <c>data-passkey-options</c>-Skript der Seite.</summary>
    public static string PasskeyOptions(string html) =>
        PasskeyOptionsPattern().Match(html) is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Keine Passkey-Optionen im HTML.");

    /// <summary>Legt über die Manage-Seite einen Passkey des Authenticators an.</summary>
    public static async Task<(HttpResponseMessage Response, string Html)> AddPasskeyAsync(FormClient client, SoftwareAuthenticator authenticator)
    {
        var (_, page) = await client.GetAsync("/Account/Manage");
        return await client.PostWithTokenAsync(
            "/Account/Manage?handler=AddPasskey",
            new Dictionary<string, string> { ["Credential"] = authenticator.Create(PasskeyOptions(page)) },
            FormClient.AntiforgeryToken(page));
    }

    public static async Task<(HttpResponseMessage Response, string Html)> PasskeyLoginAsync(
        FormClient client, SoftwareAuthenticator authenticator, string? returnUrl = null)
    {
        var (_, page) = await client.GetAsync("/Account/Login");
        return await client.PostWithTokenAsync(
            "/Account/Login?handler=Passkey",
            new Dictionary<string, string> { ["Credential"] = authenticator.Get(PasskeyOptions(page)), ["ReturnUrl"] = returnUrl ?? "" },
            FormClient.AntiforgeryToken(page));
    }

    public static string PasskeyId(byte[] credentialId) => WebEncoders.Base64UrlEncode(credentialId);

    public static Task<(HttpResponseMessage Response, string Html)> RemovePasskeyAsync(FormClient client, string passkeyId) =>
        client.PostAsync("/Account/Manage", "/Account/Manage?handler=RemovePasskey", new Dictionary<string, string> { ["PasskeyId"] = passkeyId });

    public static Task<(HttpResponseMessage Response, string Html)> SetPasswordAsync(FormClient client, string password, string? confirm = null) =>
        client.PostAsync("/Account/Manage", "/Account/Manage?handler=SetPassword", new Dictionary<string, string>
        {
            ["NewPassword"] = password,
            ["ConfirmNewPassword"] = confirm ?? password,
        });

    /// <summary>Legt einen bestätigten Account an und meldet ihn an (Passwort <see cref="Password"/>).</summary>
    public static async Task<(FormClient Client, CreatedAccount Account)> SignInAsync(GameServerFactory factory, string? language = null)
    {
        var account = await CreateConfirmedAccountAsync(factory, language);
        var client = factory.CreateFormClient();
        await LoginAsync(client, account.Email);
        return (client, account);
    }

    /// <summary>Hinterlegt einen Passkey direkt im Account (ohne Zeremonie) und gibt die Credential-ID zurück.</summary>
    public static async Task<byte[]> AddStoredPasskeyAsync(GameServerFactory factory, string email, DateTimeOffset? createdAt = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var user = (await users.FindByEmailAsync(email))!;
        var credentialId = RandomNumberGenerator.GetBytes(16);
        var result = await users.AddOrUpdatePasskeyAsync(user, new UserPasskeyInfo(
            credentialId: credentialId,
            publicKey: [1, 2, 3],
            createdAt: createdAt ?? DateTimeOffset.UtcNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: false,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []));
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return credentialId;
    }

    /// <summary>Entfernt das Passwort ohne den Security-Stamp zu ändern, damit eine laufende Sitzung bleibt.</summary>
    public static async Task RemovePasswordDirectlyAsync(GameServerFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var user = (await users.FindByEmailAsync(email))!;
        user.PasswordHash = null;
        Assert.True((await users.UpdateAsync(user)).Succeeded);
    }

    public static async Task<int> PasskeyCountAsync(GameServerFactory factory, string email)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        return (await users.GetPasskeysAsync((await users.FindByEmailAsync(email))!)).Count;
    }

    /// <summary>Der Link (Pfad und Query) aus dem Text einer Mail.</summary>
    public static string LinkFromMail(string mailText) =>
        LinkPattern().Match(mailText) is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Kein Link in der Mail:\n" + mailText);

    [GeneratedRegex("<script type=\"application/json\" data-passkey-options>(.*?)</script>", RegexOptions.Singleline)]
    private static partial Regex PasskeyOptionsPattern();

    [GeneratedRegex(@"https?://[^/\s]+(/\S+)")]
    private static partial Regex LinkPattern();
}
