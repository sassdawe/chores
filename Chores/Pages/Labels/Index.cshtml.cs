using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public IndexModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    public List<Label> Labels { get; set; } = [];

    public async Task OnGetAsync()
    {
        var householdIds = await _householdMemberships.GetHouseholdIdsAsync(User.Identity!.Name);
        if (householdIds.Count == 0) return;

        Labels = await _db.Labels
            .Include(l => l.Household)
            .Where(l => householdIds.Contains(l.HouseholdId))
            .OrderBy(l => l.Household.Name)
            .ThenBy(l => l.Name)
            .ToListAsync();
    }
}
