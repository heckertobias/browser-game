using System.ComponentModel.DataAnnotations;
using BrowserGame.Admin.Auth;
using BrowserGame.Admin.Resources;
using BrowserGame.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace BrowserGame.Admin.Pages;

/// <summary>Admins einladen und entfernen (004 AK-4e, AK-4f, AK-12).</summary>
public sealed class AdminsModel(
    UserManager<AdminUser> users, AdminCredentials credentials, AdminInvites invites, SmtpMailer mailer, AuditLog audit,
    IStringLocalizer<AdminTexts> t) : PageModel
{
    public sealed record AdminRow(string Id, string Name, string Email, bool Active, bool IsMe);

    [BindProperty]
    [EmailAddress(ErrorMessage = "Validation.EmailInvalid")]
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
        var hours = AdminInvites.Lifetime.TotalHours;
        Message = t["Admins.InviteCreated", hours];

        if (!string.IsNullOrWhiteSpace(InviteEmail))
        {
            var text = t["Mail.Invite.Body", me, link, hours].Value;
            var result = await mailer.SendAsync(InviteEmail, t["Mail.Invite.Subject"], text, MailHtml.FromText(text, link),
                HttpContext.RequestAborted);
            Message += " " + (result.Status == MailStatus.Sent
                ? t["Admins.InviteMailed", InviteEmail]
                : t["Admins.InviteMailFailed", result.Error ?? ""]).Value;
        }

        await audit.WriteAsync(me, t["AuditEntry.AdminInvited"],
            string.IsNullOrWhiteSpace(InviteEmail) ? t["AuditEntry.InviteLinkOnly"] : t["AuditEntry.To", InviteEmail]);
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
            ModelState.AddModelError("", t["Admins.CannotRemoveSelf"]);
        }
        else if (await credentials.IsActiveAsync(target) && (await credentials.ActiveAdminIdsAsync()).Count <= 1)
        {
            ModelState.AddModelError("", t["Admins.CannotRemoveLast"]);
        }
        else
        {
            await users.DeleteAsync(target);
            await audit.WriteAsync(User.Identity!.Name!, t["AuditEntry.AdminRemoved"], target.UserName);
            Message = t["Admins.Removed", target.UserName!];
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
