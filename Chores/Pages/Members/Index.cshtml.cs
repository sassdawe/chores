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
    public const int MaxSpaceNameLength = 100;

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
    [BindProperty]
    public int? SelectedHouseholdId { get; set; }
    public string? SelectedHouseholdName { get; set; }
    public bool CanInviteMembers { get; set; }

    [BindProperty]
    public string SpaceName { get; set; } = string.Empty;

    [BindProperty]
    public string RenameSpaceName { get; set; } = string.Empty;

    [BindProperty]
    public string InviteLoginName { get; set; } = string.Empty;

    [TempData]
    public string? InviteResult { get; set; }

    public async Task OnGetAsync([FromQuery] int? householdId)
    {
        await LoadAsync(householdId);
    }

    public async Task<IActionResult> OnPostInviteAsync([FromQuery] int? householdId)
    {
        var targetHouseholdId = householdId ?? SelectedHouseholdId;

        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();
        if (targetHouseholdId is null) return NotFound();
        if (!await _householdMemberships.IsOwnerAsync(currentUser.LoginName, targetHouseholdId.Value)) return Forbid();

        if (!LoginNameValidator.TryNormalize(InviteLoginName, out var normalizedLoginName))
        {
            ModelState.AddModelError(nameof(InviteLoginName), "Enter a valid login name.");
            await LoadAsync(targetHouseholdId);
            return Page();
        }

        await _householdInvitations.CreateInviteAsync(currentUser, targetHouseholdId.Value, normalizedLoginName);
        InviteResult = "Invite sent.";
        return RedirectToPage(new { householdId = targetHouseholdId });
    }

    public async Task<IActionResult> OnPostCreateSpaceAsync()
    {
        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();

        var validation = await ValidateSpaceNameAsync(SpaceName, nameof(SpaceName), currentUser.Id, excludedHouseholdId: null);
        if (!validation.IsValid)
        {
            await LoadAsync(SelectedHouseholdId);
            return Page();
        }

        var household = new Household { Name = validation.NormalizedName };
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

    public async Task<IActionResult> OnPostRenameSpaceAsync([FromQuery] int? householdId)
    {
        var targetHouseholdId = householdId ?? SelectedHouseholdId;

        var currentUser = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (currentUser is null) return NotFound();
        if (targetHouseholdId is null) return NotFound();
        if (!await _householdMemberships.IsOwnerAsync(currentUser.LoginName, targetHouseholdId.Value)) return Forbid();

        var household = await _db.Households.FirstOrDefaultAsync(candidate => candidate.Id == targetHouseholdId.Value);
        if (household is null) return NotFound();

        var validation = await ValidateSpaceNameAsync(RenameSpaceName, nameof(RenameSpaceName), currentUser.Id, household.Id);
        if (!validation.IsValid)
        {
            await LoadAsync(targetHouseholdId);
            return Page();
        }

        household.Name = validation.NormalizedName;
        await _db.SaveChangesAsync();

        return RedirectToPage(new { householdId = household.Id });
    }

    private async Task LoadAsync(int? householdId)
    {
        var username = User.Identity!.Name;
        Spaces = await _householdMemberships.GetMembershipsAsync(username);
        if (Spaces.Count == 0)
        {
            Members = [];
            SelectedHouseholdId = null;
            SelectedHouseholdName = null;
            CanInviteMembers = false;
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

    private async Task<(bool IsValid, string NormalizedName)> ValidateSpaceNameAsync(
        string submittedName,
        string propertyName,
        int userId,
        int? excludedHouseholdId)
    {
        var normalizedName = NormalizeSpaceName(submittedName);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            ModelState.AddModelError(propertyName, "Name is required.");
            return (false, normalizedName);
        }

        if (normalizedName.Any(char.IsControl))
        {
            ModelState.AddModelError(propertyName, "Name can't contain control characters.");
            return (false, normalizedName);
        }

        if (normalizedName.Length > MaxSpaceNameLength)
        {
            ModelState.AddModelError(propertyName, $"Name must be {MaxSpaceNameLength} characters or fewer.");
            return (false, normalizedName);
        }

        if (await HasSpaceNamedAsync(userId, normalizedName, excludedHouseholdId))
        {
            ModelState.AddModelError(propertyName, "You already have a space with that name.");
            return (false, normalizedName);
        }

        return (true, normalizedName);
    }

    private async Task<bool> HasSpaceNamedAsync(int userId, string normalizedName, int? excludedHouseholdId)
    {
        var householdNames = await _db.HouseholdMemberships
            .AsNoTracking()
            .Include(membership => membership.Household)
            .Where(membership => membership.UserId == userId
                && (!excludedHouseholdId.HasValue || membership.HouseholdId != excludedHouseholdId.Value))
            .Select(membership => membership.Household.Name)
            .ToListAsync();

        return householdNames.Any(name => string.Equals(name, normalizedName, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeSpaceName(string value)
    {
        return string.Join(' ', value.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
