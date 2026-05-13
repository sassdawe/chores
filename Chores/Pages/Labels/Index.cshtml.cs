using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public List<Label> Labels { get; set; } = [];

    public async Task OnGetAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return;

        Labels = await _db.Labels
            .Where(l => l.HouseholdId == user.HouseholdId)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }
}
