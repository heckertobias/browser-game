using BrowserGame.Server.Features.Game;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Features.Accounts.Presentation.Account;

/// <summary>Abmelden beendet die Sitzung auf allen Geräten (neuer Security-Stamp) – 003 AK-13.</summary>
public sealed class LogoutModel(SignInManager<PlayerAccount> signIn, UserManager<PlayerAccount> users, GameLoop game) : PageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        if (await users.GetUserAsync(User) is { } user)
        {
            await users.UpdateSecurityStampAsync(user);
            // Figur sofort weg, laufende Spiel-Verbindung beenden (AK-22).
            game.ForceLeave(user.Id, savePosition: true);
        }

        await signIn.SignOutAsync();
        return Redirect("/");
    }
}
