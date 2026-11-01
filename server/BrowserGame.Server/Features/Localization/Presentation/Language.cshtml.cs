using BrowserGame.Server.Features.Localization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PlayerAccount = BrowserGame.Data.Account;

namespace BrowserGame.Server.Features.Localization.Presentation;

/// <summary>Sprache umschalten (006 AK-3, AK-5): Cookie setzen, bei angemeldeten Spielern auch im Account speichern.</summary>
public sealed partial class LanguageModel(UserManager<PlayerAccount> users, ILogger<LanguageModel> logger) : PageModel
{
    [BindProperty]
    public string? Language { get; set; }

    [BindProperty]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync()
    {
        var code = GameLanguages.Normalize(Language);
        LanguageCookie.Append(Response, code);

        // Kein neuer Security-Stamp: Angemeldete Geräte bleiben angemeldet.
        if (await users.GetUserAsync(User) is { } user && user.Language != code)
        {
            user.Language = code;
            var result = await users.UpdateAsync(user);
            if (!result.Succeeded)
            {
                LogUpdateFailed(user.Id, string.Join("; ", result.Errors.Select(e => e.Code)));
            }
        }

        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl! : "/");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Sprache von Account {AccountId} konnte nicht gespeichert werden: {Errors}")]
    private partial void LogUpdateFailed(string accountId, string errors);
}
