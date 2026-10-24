using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Pages;

/// <summary>Admins einladen und entfernen (004 AK-4e, AK-4f, AK-12).</summary>
public sealed class AdminsModel(
    UserManager<AdminUser> users, AdminCredentials credentials, AdminInvites invites, SmtpMailer mailer, AuditLog audit) : PageModel
{
    public sealed record AdminRow(string Id, string Name, string Email, bool Active, bool IsMe);

    [BindProperty]
    [EmailAddress(ErrorMessage = "Keine gültige E-Mail-Adresse.")]
    public string? InviteEmail { get; set; }

    [BindProperty]
    public string? RemoveId { get; set; }

    public IReadOnlyList<AdminRow> Admins { get; private set; } = [];

    public string? InviteLink { get; private set; }

    public string? Message { get; private set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostInviteAsync()
    {
        ModelState.Remove(nameof(RemoveId));
        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        var me = User.Identity!.Name!;
        var (_, link) = await invites.CreateAsync(me, InviteEmail);
        InviteLink = link;
        Message = "Einladung erstellt. Der Link ist 48 Stunden gültig und funktioniert nur einmal.";

        if (!string.IsNullOrWhiteSpace(InviteEmail))
        {
            var result = await mailer.SendAsync(InviteEmail, "Einladung als Admin für Browser Game",
                $"Hallo,\n\n{me} lädt dich als Admin für Browser Game ein. Über diesen Link richtest du deinen Zugang ein:\n{link}\n\n"
                + "Der Link ist 48 Stunden gültig und funktioniert nur einmal.",
                MailHtml.FromText(
                    $"Hallo,\n\n{me} lädt dich als Admin für Browser Game ein. Über diesen Link richtest du deinen Zugang ein:\n{link}\n\n"
                    + "Der Link ist 48 Stunden gültig und funktioniert nur einmal.", link),
                HttpContext.RequestAborted);
            Message += result.Status == MailStatus.Sent
                ? $" Mail an {InviteEmail} verschickt."
                : $" Mail nicht verschickt ({result.Error}) – gib den Link selbst weiter.";
        }

        await audit.WriteAsync(me, "Admin eingeladen", string.IsNullOrWhiteSpace(InviteEmail) ? "Link" : $"an {InviteEmail}");
        await LoadAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostRemoveAsync()
    {
        ModelState.Clear();
        var target = RemoveId is null ? null : await users.FindByIdAsync(RemoveId);
        if (target is null)
        {
            await LoadAsync();
            return Page();
        }

        if (target.Id == users.GetUserId(User))
        {
            ModelState.AddModelError("", "Du kannst dich nicht selbst entfernen.");
        }
        else if (await credentials.IsActiveAsync(target) && (await credentials.ActiveAdminIdsAsync()).Count <= 1)
        {
            ModelState.AddModelError("", "Der letzte Admin kann nicht entfernt werden.");
        }
        else
        {
            await users.DeleteAsync(target);
            await audit.WriteAsync(User.Identity!.Name!, "Admin entfernt", target.UserName);
            Message = $"{target.UserName} wurde entfernt.";
        }

        await LoadAsync();
        return Page();
    }

    private async Task LoadAsync()
    {
        var active = (await credentials.ActiveAdminIdsAsync()).ToHashSet();
        var me = users.GetUserId(User);
        Admins = await users.Users.OrderBy(u => u.UserName)
            .Select(u => new { u.Id, u.UserName, u.Email })
            .AsAsyncEnumerable()
            .Select(u => new AdminRow(u.Id, u.UserName ?? "", u.Email ?? "", active.Contains(u.Id), u.Id == me))
            .ToListAsync();
    }
}
