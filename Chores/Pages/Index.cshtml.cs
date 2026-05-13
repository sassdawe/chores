using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ScheduleAdherenceService _adherence;

    public IndexModel(AppDbContext db, ScheduleAdherenceService adherence)
    {
        _db = db;
        _adherence = adherence;
    }

    public List<ChoreStatus> ChoreStatuses { get; set; } = [];
    public List<Label> AllLabels { get; set; } = [];
    public int? ActiveLabelId { get; set; }

    public async Task OnGetAsync([FromQuery] int? labelId)
    {
        ActiveLabelId = labelId;

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return;

        AllLabels = await _db.Labels
            .Where(l => l.HouseholdId == user.HouseholdId)
            .OrderBy(l => l.Name)
            .ToListAsync();

        var choresQuery = _db.Chores
            .Include(c => c.Labels)
            .Where(c => c.HouseholdId == user.HouseholdId);

        if (labelId.HasValue)
            choresQuery = choresQuery.Where(c => c.Labels.Any(l => l.Id == labelId.Value));

        var chores = await choresQuery
            .OrderBy(c => c.Name)
            .ToListAsync();

        var choreIds = chores.Select(c => c.Id).ToList();

        var latestRecords = await _db.CompletionRecords
            .Where(r => choreIds.Contains(r.ChoreId))
            .GroupBy(r => r.ChoreId)
            .Select(g => g.OrderByDescending(r => r.CompletedAtUtc).First())
            .ToListAsync();

        var lastByChore = latestRecords.ToDictionary(r => r.ChoreId);

        ChoreStatuses = chores.Select(c =>
        {
            lastByChore.TryGetValue(c.Id, out var last);
            var adherence = _adherence.Evaluate(c.Schedule, last?.CompletedAtUtc);
            return new ChoreStatus(c, adherence, last?.CompletedAtUtc);
        }).ToList();
    }

    public record ChoreStatus(Chore Chore, ScheduleAdherence Adherence, DateTime? LastCompletedUtc);
}
