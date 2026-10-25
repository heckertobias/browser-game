using System.Text.RegularExpressions;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;

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

    public static Task<(HttpResponseMessage Response, string Html)> RegisterAsync(
        FormClient client, string email, string name, string password = Password, string? confirm = null) =>
        client.PostAsync("/Account/Register", "/Account/Register", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Name"] = name,
            ["Password"] = password,
            ["ConfirmPassword"] = confirm ?? password,
        });

    public static Task<(HttpResponseMessage Response, string Html)> LoginAsync(
        FormClient client, string email, string password = Password, string? returnUrl = null) =>
        client.PostAsync("/Account/Login", "/Account/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl ?? "",
        });

    /// <summary>Legt einen Account mit bestätigter E-Mail-Adresse direkt über Identity an.</summary>
    public static async Task<CreatedAccount> CreateConfirmedAccountAsync(GameServerFactory factory)
    {
        var account = new CreatedAccount(UniqueEmail(), UniqueName());
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<Account>>();
        var result = await users.CreateAsync(new Account { UserName = account.Name, Email = account.Email, EmailConfirmed = true }, Password);
        Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(e => e.Description)));
        return account;
    }

    /// <summary>Der Link (Pfad und Query) aus dem Text einer Mail.</summary>
    public static string LinkFromMail(string mailText) =>
        LinkPattern().Match(mailText) is { Success: true } match
            ? match.Groups[1].Value
            : throw new InvalidOperationException("Kein Link in der Mail:\n" + mailText);

    [GeneratedRegex(@"https?://[^/\s]+(/\S+)")]
    private static partial Regex LinkPattern();
}
