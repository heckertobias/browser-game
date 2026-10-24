using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BrowserGame.Admin.Pages;

public sealed class LogoutModel(SignInManager<AdminUser> signIn) : PageModel
{
    public IActionResult OnGet() => RedirectToPage("Index");

    public async Task<IActionResult> OnPostAsync()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("Login");
    }
}
