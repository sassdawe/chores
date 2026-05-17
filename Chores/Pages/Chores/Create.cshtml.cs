using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public CreateModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public Schedule Schedule { get; set; } = Schedule.Weekly;

    [BindProperty]
    public List<int> SelectedLabelIds { get; set; } = [];

    [BindProperty]
    public int HouseholdId { get; set; }

    public List<HouseholdMembership> Spaces { get; set; } = [];
    public List<Label> AllAvailableLabels { get; set; } = [];
    public IReadOnlyList<Label> AvailableLabels =>
        [.. AllAvailableLabels
            .Where(label => label.HouseholdId == HouseholdId)
            .OrderBy(label => label.Name)];

    public async Task OnGetAsync([FromQuery] int? householdId)
    {
        await LoadSpacesAsync(householdId);
        await LoadAvailableLabelsAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var submittedHouseholdId = HouseholdId;
        await LoadSpacesAsync(submittedHouseholdId);
        await LoadAvailableLabelsAsync();

        if (Spaces.Count == 0)
        {
            ModelState.AddModelError(nameof(HouseholdId), "Create a space before adding chores.");
            return Page();
        }

        if (!Spaces.Any(space => space.HouseholdId == submittedHouseholdId))
        {
            ModelState.AddModelError(nameof(HouseholdId), "Select a space you can access.");
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        var chore = new Chore
        {
            Name = Name.Trim(),
            Schedule = Schedule,
            HouseholdId = HouseholdId
        };

        if (SelectedLabelIds.Count > 0)
        {
            var labels = await _db.Labels
                .Where(l => SelectedLabelIds.Contains(l.Id) && l.HouseholdId == HouseholdId)
                .ToListAsync();
            foreach (var label in labels)
                chore.Labels.Add(label);
        }

        _db.Chores.Add(chore);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadSpacesAsync(int? householdId = null)
    {
        Spaces = await _householdMemberships.GetMembershipsAsync(User.Identity!.Name);
        HouseholdId = householdId.HasValue && Spaces.Any(space => space.HouseholdId == householdId.Value)
            ? householdId.Value
            : Spaces.FirstOrDefault()?.HouseholdId ?? 0;
    }

    private async Task LoadAvailableLabelsAsync()
    {
        var accessibleHouseholdIds = Spaces.Select(space => space.HouseholdId).ToList();
        if (accessibleHouseholdIds.Count == 0)
        {
            AllAvailableLabels = [];
            return;
        }

        AllAvailableLabels = await _db.Labels
            .Where(label => accessibleHouseholdIds.Contains(label.HouseholdId))
            .OrderBy(l => l.Name)
            .ToListAsync();
    }
}
