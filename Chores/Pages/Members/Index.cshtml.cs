using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Members;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public List<AppUser> Members { get; set; } = [];

    [BindProperty]
    public string InviteLoginName { get; set; } = string.Empty;

    public string? InviteResult { get; set; }

    public async Task OnGetAsync()
    {
        await LoadMembersAsync();
    }

    public async Task<IActionResult> OnPostInviteAsync()
    {
        await LoadMembersAsync();

        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(InviteLoginName))
        {
            var target = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == InviteLoginName.Trim());
            if (target is not null && target.HouseholdId != currentUser.HouseholdId)
            {
                target.HouseholdId = currentUser.HouseholdId;
                await _db.SaveChangesAsync();
                await LoadMembersAsync();
            }
        }

        InviteResult = "Invite sent.";
        return Page();
    }

    private async Task LoadMembersAsync()
    {
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return;

        Members = await _db.Users
            .Where(u => u.HouseholdId == currentUser.HouseholdId)
            .OrderBy(u => u.LoginName)
            .ToListAsync();
    }
}
