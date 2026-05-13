using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

public record CompletionEntry(CompletionRecord Record, ScheduleAdherence? Adherence);

[Authorize]
public class HistoryModel(AppDbContext db, ScheduleAdherenceService adherenceService) : PageModel
{
    public Chore Chore { get; set; } = null!;
    public List<CompletionEntry> Entries { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return Forbid();

        var chore = await db.Chores
            .FirstOrDefaultAsync(c => c.Id == id && c.HouseholdId == user.HouseholdId);
        if (chore is null) return NotFound();

        Chore = chore;

        var records = await db.CompletionRecords
            .Include(r => r.CompletedByUser)
            .Where(r => r.ChoreId == id)
            .OrderByDescending(r => r.CompletedAtUtc)
            .ToListAsync();

        // Pair each record with adherence relative to the previous completion.
        // Records are newest-first, so records[i+1] is the prior completion.
        // The oldest record has no predecessor — adherence is null ("First completion").
        Entries = records
            .Select((record, index) =>
            {
                if (index == records.Count - 1)
                    return new CompletionEntry(record, null); // first ever

                var previous = records[index + 1].CompletedAtUtc;
                var adherence = adherenceService.Evaluate(chore.Schedule, previous, record.CompletedAtUtc);
                return new CompletionEntry(record, adherence);
            })
            .ToList();

        return Page();
    }
}
