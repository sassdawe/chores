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
    private readonly HouseholdMembershipService _householdMemberships;

    public IndexModel(
        AppDbContext db,
        HouseholdInvitationService householdInvitations,
        HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdInvitations = householdInvitations;
        _householdMemberships = householdMemberships;
    }

    public List<HouseholdMembership> Spaces { get; set; } = [];
    public List<HouseholdMembership> Members { get; set; } = [];
    public int? SelectedHouseholdId { get; set; }
    public string? SelectedHouseholdName { get; set; }
    public bool CanInviteMembers { get; set; }

    [BindProperty]
    public string SpaceName { get; set; } = string.Empty;

    [BindProperty]
    public string InviteLoginName { get; set; } = string.Empty;

    [TempData]
    public string? InviteResult { get; set; }

    public async Task OnGetAsync([FromQuery] int? householdId)
    {
        await LoadAsync(householdId);
    }

    public async Task<IActionResult> OnPostInviteAsync()
    {
        await LoadAsync(SelectedHouseholdId);

        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();
        if (SelectedHouseholdId is null) return NotFound();
        if (!await _householdMemberships.IsOwnerAsync(currentUser.LoginName, SelectedHouseholdId.Value)) return Forbid();

        if (!LoginNameValidator.TryNormalize(InviteLoginName, out var normalizedLoginName))
        {
            ModelState.AddModelError(nameof(InviteLoginName), "Enter a valid login name.");
            return Page();
        }

        await _householdInvitations.CreateInviteAsync(currentUser, SelectedHouseholdId.Value, normalizedLoginName);
        InviteResult = "Invite sent.";
        return RedirectToPage(new { householdId = SelectedHouseholdId });
    }

    public async Task<IActionResult> OnPostCreateSpaceAsync()
    {
        if (string.IsNullOrWhiteSpace(SpaceName))
        {
            ModelState.AddModelError(nameof(SpaceName), "Name is required.");
            await LoadAsync(SelectedHouseholdId);
            return Page();
        }

        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();

        var household = new Household { Name = SpaceName.Trim() };
        _db.Households.Add(household);
        _db.HouseholdMemberships.Add(new HouseholdMembership
        {
            UserId = currentUser.Id,
            Household = household,
            IsOwner = true,
            JoinedAtUtc = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        return RedirectToPage(new { householdId = household.Id });
    }

    private async Task LoadAsync(int? householdId)
    {
        var username = User.Identity!.Name;
        Spaces = await _householdMemberships.GetMembershipsAsync(username);
        if (Spaces.Count == 0)
        {
            return;
        }

        var selectedMembership = householdId.HasValue
            ? Spaces.FirstOrDefault(membership => membership.HouseholdId == householdId.Value)
            : Spaces.First();
        selectedMembership ??= Spaces.First();
        SelectedHouseholdId = selectedMembership.HouseholdId;
        SelectedHouseholdName = selectedMembership.Household.Name;
        CanInviteMembers = selectedMembership.IsOwner;

        Members = await _db.HouseholdMemberships
            .AsNoTracking()
            .Include(membership => membership.User)
            .Where(membership => membership.HouseholdId == selectedMembership.HouseholdId)
            .OrderBy(membership => membership.User.LoginName)
            .ToListAsync();
    }
}
