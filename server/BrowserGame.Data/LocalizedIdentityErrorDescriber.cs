using System.Globalization;
using System.Resources;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Data;

/// <summary>Texte für die Identity-Fehler, die Spieler zu sehen bekommen, in der Sprache der Anfrage (ADR 0008).</summary>
public sealed class LocalizedIdentityErrorDescriber : IdentityErrorDescriber
{
    internal static ResourceManager ResourceManager { get; } = new("BrowserGame.Data.Resources.IdentityErrors", typeof(LocalizedIdentityErrorDescriber).Assembly);

    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), userName);

    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName));

    // Bewusst neutral: Registrierung verrät nicht, ob es eine Adresse schon gibt.
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail));

    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail));

    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), length);

    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch));

    public override IdentityError InvalidToken() => Error(nameof(InvalidToken));

    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword));

    public override IdentityError DefaultError() => Error(nameof(DefaultError));

    private static IdentityError Error(string code, params object[] args)
    {
        var text = ResourceManager.GetString(code, CultureInfo.CurrentUICulture) ?? code;
        return new IdentityError
        {
            Code = code,
            Description = args.Length == 0 ? text : string.Format(CultureInfo.CurrentCulture, text, args),
        };
    }
}
