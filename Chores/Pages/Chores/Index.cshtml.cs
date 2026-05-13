using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;

    public IndexModel(AppDbContext db) => _db = db;

    public List<Chore> Chores { get; set; } = [];

    public async Task OnGetAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return;

        Chores = await _db.Chores
            .Where(c => c.HouseholdId == user.HouseholdId)
            .OrderBy(c => c.Name)
            .ToListAsync();
    }
}
