using BrowserGame.Server.Data;
using Microsoft.AspNetCore.Identity;

namespace BrowserGame.Server.Accounts;

/// <summary>Erzeugt Token und Link und verschickt die passende Mail (AK-2, AK-3, AK-14).</summary>
internal sealed class AccountEmails(UserManager<Account> users, GameEmailSender mail, AccountLinks links)
{
    public async Task SendConfirmationAsync(Account account)
    {
        var token = AccountLinks.EncodeToken(await users.GenerateEmailConfirmationTokenAsync(account));
        var link = links.Absolute($"/Account/ConfirmEmail?userId={Uri.EscapeDataString(account.Id)}&code={token}");
        await mail.SendConfirmationLinkAsync(account, account.Email!, link);
    }

    public async Task SendPasswordResetAsync(Account account)
    {
        var token = AccountLinks.EncodeToken(await users.GeneratePasswordResetTokenAsync(account));
        var link = links.Absolute($"/Account/ResetPassword?userId={Uri.EscapeDataString(account.Id)}&code={token}");
        await mail.SendPasswordResetLinkAsync(account, account.Email!, link);
    }

    public Task SendAlreadyRegisteredAsync(Account account) =>
        mail.SendAlreadyRegisteredAsync(account, account.Email!, links.Absolute("/Account/Login"), links.Absolute("/Account/ForgotPassword"));
}
