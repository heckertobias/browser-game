using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Accounts;

/// <summary>Deutsche Texte für die Identity-Fehler, die Spieler zu sehen bekommen.</summary>
internal sealed class GermanIdentityErrorDescriber : IdentityErrorDescriber
{
    public override IdentityError DuplicateUserName(string userName) =>
        new() { Code = nameof(DuplicateUserName), Description = $"Der Name „{userName}“ ist schon vergeben." };

    public override IdentityError InvalidUserName(string? userName) =>
        new() { Code = nameof(InvalidUserName), Description = PlayerNames.Describe(PlayerNameError.Characters) };

    // Bewusst neutral: Registrierung verrät nicht, ob es eine Adresse schon gibt.
    public override IdentityError DuplicateEmail(string email) =>
        new() { Code = nameof(DuplicateEmail), Description = "Die Registrierung hat nicht geklappt. Bitte versuche es erneut." };

    public override IdentityError InvalidEmail(string? email) =>
        new() { Code = nameof(InvalidEmail), Description = "Das ist keine gültige E-Mail-Adresse." };

    public override IdentityError PasswordTooShort(int length) =>
        new() { Code = nameof(PasswordTooShort), Description = $"Das Passwort muss mindestens {length} Zeichen lang sein." };

    public override IdentityError PasswordMismatch() =>
        new() { Code = nameof(PasswordMismatch), Description = "Das Passwort stimmt nicht." };

    public override IdentityError InvalidToken() =>
        new() { Code = nameof(InvalidToken), Description = "Der Link ist ungültig oder abgelaufen." };

    public override IdentityError UserAlreadyHasPassword() =>
        new() { Code = nameof(UserAlreadyHasPassword), Description = "Für diesen Account ist schon ein Passwort gesetzt." };

    public override IdentityError DefaultError() =>
        new() { Code = nameof(DefaultError), Description = "Etwas ist schiefgegangen. Bitte versuche es erneut." };
}
