using BrowserGame.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace BrowserGame.Admin.Pages;

/// <summary>Protokoll der Admin-Aktionen, neueste zuerst (004 AK-12).</summary>
public sealed class AuditModel(AdminDbContext db) : PageModel
{
    public const int Limit = 200;

    public IReadOnlyList<AdminAuditEntry> Entries { get; private set; } = [];

    public async Task OnGetAsync() =>
        Entries = await db.AuditEntries.AsNoTracking().OrderByDescending(e => e.Id).Take(Limit).ToListAsync(HttpContext.RequestAborted);
}
