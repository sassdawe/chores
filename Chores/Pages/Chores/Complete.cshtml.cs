using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class CompleteModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public CompleteModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    public Chore Chore { get; set; } = null!;

    [BindProperty]
    public DateTime CompletedAt { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var chore = await _db.Chores.FirstOrDefaultAsync(c => c.Id == id);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId)) return NotFound();

        Chore = chore;
        CompletedAt = DateTime.Now;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var chore = await _db.Chores.FirstOrDefaultAsync(c => c.Id == id);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(user.LoginName, chore.HouseholdId)) return NotFound();

        // Clamp to now — never allow future completions
        var completedAtUtc = DateTime.SpecifyKind(CompletedAt, DateTimeKind.Local).ToUniversalTime();
        if (completedAtUtc > DateTime.UtcNow)
            completedAtUtc = DateTime.UtcNow;

        _db.CompletionRecords.Add(new CompletionRecord
        {
            ChoreId = chore.Id,
            CompletedByUserId = user.Id,
            CompletedAtUtc = completedAtUtc
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("/Index");
    }
}
