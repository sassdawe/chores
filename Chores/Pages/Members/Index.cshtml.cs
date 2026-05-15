using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Members;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdInvitationService _householdInvitations;

    public IndexModel(AppDbContext db, HouseholdInvitationService householdInvitations)
    {
        _db = db;
        _householdInvitations = householdInvitations;
    }

    public List<AppUser> Members { get; set; } = [];
    public bool CanInviteMembers { get; set; }

    [BindProperty]
    public string InviteLoginName { get; set; } = string.Empty;

    [TempData]
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
        if (!currentUser.IsHouseholdOwner) return Forbid();

        if (!LoginNameValidator.TryNormalize(InviteLoginName, out var normalizedLoginName))
        {
            ModelState.AddModelError(nameof(InviteLoginName), "Enter a valid login name.");
            return Page();
        }

        await _householdInvitations.CreateInviteAsync(currentUser, normalizedLoginName);
        InviteResult = "Invite sent.";
        return RedirectToPage();
    }

    private async Task LoadMembersAsync()
    {
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return;
        CanInviteMembers = currentUser.IsHouseholdOwner;

        Members = await _db.Users
            .Where(u => u.HouseholdId == currentUser.HouseholdId)
            .OrderBy(u => u.LoginName)
            .ToListAsync();
    }
}
