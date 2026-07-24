using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Chores.Pages.Chores;

[Authorize]
public class MoveLabelsModel(
    AppDbContext db,
    HouseholdMembershipService householdMemberships) : PageModel
{
    [BindProperty]
    public int ChoreId { get; set; }

    [BindProperty]
    public List<int> SelectedLabelIds { get; set; } = [];

    public string ChoreName { get; set; } = string.Empty;
    public string HouseholdName { get; set; } = string.Empty;
    public List<Label> AvailableLabels { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadPageAsync(id);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var chore = await db.Chores
            .Include(candidate => candidate.Labels)
            .Include(candidate => candidate.Household)
            .FirstOrDefaultAsync(candidate => candidate.Id == ChoreId);
        if (chore is null)
        {
            return NotFound();
        }

        if (!await householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId))
        {
            return NotFound();
        }

        chore.Labels.Clear();

        if (SelectedLabelIds.Count > 0)
        {
            var labels = await db.Labels
                .Where(label => SelectedLabelIds.Contains(label.Id) && label.HouseholdId == chore.HouseholdId)
                .ToListAsync();
            foreach (var label in labels)
            {
                chore.Labels.Add(label);
            }
        }

        await db.SaveChangesAsync();
        return LocalRedirect(BuildEditPath());
    }

    public string BuildEditPath()
    {
        return $"{Request.PathBase}/Chores/Edit?id={ChoreId.ToString(CultureInfo.InvariantCulture)}";
    }

    private async Task<IActionResult> LoadPageAsync(int choreId)
    {
        var chore = await db.Chores
            .Include(candidate => candidate.Labels)
            .Include(candidate => candidate.Household)
            .FirstOrDefaultAsync(candidate => candidate.Id == choreId);
        if (chore is null)
        {
            return NotFound();
        }

        if (!await householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId))
        {
            return NotFound();
        }

        ChoreId = chore.Id;
        ChoreName = chore.Name;
        HouseholdName = chore.Household.Name;
        SelectedLabelIds = chore.Labels.Select(label => label.Id).ToList();
        AvailableLabels = await db.Labels
            .Where(label => label.HouseholdId == chore.HouseholdId)
            .OrderBy(label => label.Name)
            .ToListAsync();

        return Page();
    }
}
