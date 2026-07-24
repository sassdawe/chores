using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Chores.Pages.Chores;

[Authorize]
public class MoveModel(
    AppDbContext db,
    HouseholdMembershipService householdMemberships,
    ChoreMoveService choreMoveService) : PageModel
{
    [BindProperty]
    public int ChoreId { get; set; }

    [BindProperty]
    public int DestinationHouseholdId { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? LabelId { get; set; }

    public string ChoreName { get; set; } = string.Empty;
    public string CurrentHouseholdName { get; set; } = string.Empty;
    public string? DestinationHouseholdName { get; set; }
    public List<HouseholdMembership> DestinationSpaces { get; set; } = [];
    public bool ShowConfirmation { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        return await LoadPageAsync(id, null, showConfirmation: false);
    }

    public async Task<IActionResult> OnPostConfirmAsync()
    {
        return await LoadPageAsync(ChoreId, DestinationHouseholdId, showConfirmation: true);
    }

    public async Task<IActionResult> OnPostStartAsync()
    {
        var chore = await db.Chores
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

        var destinationSpaces = (await householdMemberships.GetMembershipsAsync(User.Identity!.Name))
            .Where(space => space.HouseholdId != chore.HouseholdId)
            .ToList();
        var destinationSpace = destinationSpaces.FirstOrDefault(space => space.HouseholdId == DestinationHouseholdId);
        if (destinationSpace is null)
        {
            ModelState.AddModelError(nameof(DestinationHouseholdId), "Select a space you can access.");
            return await LoadPageAsync(ChoreId, null, showConfirmation: false);
        }

        var moved = await choreMoveService.TryMoveAsync(ChoreId, destinationSpace.HouseholdId);
        if (!moved)
        {
            ModelState.AddModelError(string.Empty, "Unable to move this chore.");
            return await LoadPageAsync(ChoreId, destinationSpace.HouseholdId, showConfirmation: true);
        }

        return RedirectToPage("/Chores/MoveLabels", new { id = ChoreId });
    }

    public string BuildEditPath()
    {
        var queryBuilder = new QueryBuilder
        {
            { "id", ChoreId.ToString(CultureInfo.InvariantCulture) }
        };

        if (LabelId.HasValue)
        {
            queryBuilder.Add("labelId", LabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/Edit{queryBuilder.ToQueryString().Value}";
    }

    public string BuildMovePath()
    {
        var queryBuilder = new QueryBuilder
        {
            { "id", ChoreId.ToString(CultureInfo.InvariantCulture) }
        };

        if (LabelId.HasValue)
        {
            queryBuilder.Add("labelId", LabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/Move{queryBuilder.ToQueryString().Value}";
    }

    private async Task<IActionResult> LoadPageAsync(int choreId, int? destinationHouseholdId, bool showConfirmation)
    {
        var chore = await db.Chores
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
        CurrentHouseholdName = chore.Household.Name;
        DestinationSpaces = (await householdMemberships.GetMembershipsAsync(User.Identity!.Name))
            .Where(space => space.HouseholdId != chore.HouseholdId)
            .ToList();

        if (destinationHouseholdId.HasValue)
        {
            DestinationHouseholdId = destinationHouseholdId.Value;
            DestinationHouseholdName = DestinationSpaces
                .FirstOrDefault(space => space.HouseholdId == destinationHouseholdId.Value)?
                .Household.Name;
        }

        ShowConfirmation = showConfirmation && DestinationHouseholdName is not null;

        if (showConfirmation && DestinationHouseholdName is null)
        {
            ModelState.AddModelError(nameof(DestinationHouseholdId), "Select a different space you can access.");
        }

        return Page();
    }
}
