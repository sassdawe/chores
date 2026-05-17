using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

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

    public List<Chore> Chores { get; set; } = [];

    public async Task OnGetAsync()
    {
        var householdIds = await _householdMemberships.GetHouseholdIdsAsync(User.Identity!.Name);
        if (householdIds.Count == 0) return;

        Chores = await _db.Chores
            .Include(c => c.Household)
            .Where(c => householdIds.Contains(c.HouseholdId))
            .OrderBy(c => c.Household.Name)
            .ThenBy(c => c.Name)
            .ToListAsync();
    }
}
