using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>Löst den Bestätigungslink aus der Mail ein (003 AK-3).</summary>
public sealed class ConfirmEmailModel(UserManager<PlayerAccount> users) : PageModel
{
    public bool Confirmed { get; private set; }

    public async Task OnGetAsync(string? userId, string? code)
    {
        if (userId is not null && AccountLinks.DecodeToken(code) is { } token && await users.FindByIdAsync(userId) is { } user)
        {
            Confirmed = (await users.ConfirmEmailAsync(user, token)).Succeeded;
        }
    }
}
