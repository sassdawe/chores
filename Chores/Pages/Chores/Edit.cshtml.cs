using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public EditModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    [BindProperty]
    public int ChoreId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public Schedule Schedule { get; set; }

    [BindProperty]
    public List<int> SelectedLabelIds { get; set; } = [];

    public List<Label> AvailableLabels { get; set; } = [];
    public string HouseholdName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var chore = await _db.Chores
            .Include(c => c.Labels)
            .Include(c => c.Household)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId)) return NotFound();

        await LoadAvailableLabelsAsync(chore.HouseholdId);

        ChoreId = chore.Id;
        Name = chore.Name;
        Schedule = chore.Schedule;
        HouseholdName = chore.Household.Name;
        SelectedLabelIds = chore.Labels.Select(l => l.Id).ToList();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        var chore = await _db.Chores
            .Include(c => c.Labels)
            .FirstOrDefaultAsync(c => c.Id == ChoreId);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId)) return NotFound();

        chore.Name = Name.Trim();
        chore.Schedule = Schedule;
        chore.Labels.Clear();

        if (SelectedLabelIds.Count > 0)
        {
            var labels = await _db.Labels
                .Where(l => SelectedLabelIds.Contains(l.Id) && l.HouseholdId == chore.HouseholdId)
                .ToListAsync();
            foreach (var label in labels)
                chore.Labels.Add(label);
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadAvailableLabelsAsync(int householdId)
    {
        AvailableLabels = await _db.Labels
            .Where(label => label.HouseholdId == householdId)
            .OrderBy(label => label.Name)
            .ToListAsync();
    }
}
