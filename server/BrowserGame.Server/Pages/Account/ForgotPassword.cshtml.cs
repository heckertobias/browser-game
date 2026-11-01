using System.ComponentModel.DataAnnotations;
using BrowserGame.Data;
using BrowserGame.Server.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Pages.Account;

/// <summary>Link zum Zurücksetzen anfordern (003 AK-14); die Antwort verrät nicht, ob es die Adresse gibt.</summary>
[EnableRateLimiting(WebDefaults.AuthRateLimitPolicy)]
public sealed class ForgotPasswordModel(UserManager<PlayerAccount> users, AccountEmails emails) : PageModel
{
    [BindProperty]
    [Required(ErrorMessage = "Validation.EmailRequired")]
    public string Email { get; set; } = "";

    public bool Sent { get; private set; }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (await users.FindByEmailAsync(Email.Trim()) is { EmailConfirmed: true } user)
        {
            await emails.SendPasswordResetAsync(user);
        }

        Sent = true;
        return Page();
    }
}
