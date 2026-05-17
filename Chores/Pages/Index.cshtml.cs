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
    private readonly HouseholdMembershipService _householdMemberships;

    public IndexModel(
        AppDbContext db,
        ScheduleAdherenceService adherence,
        HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _adherence = adherence;
        _householdMemberships = householdMemberships;
    }

    public List<ChoreStatus> ChoreStatuses { get; set; } = [];
    public List<Label> AllLabels { get; set; } = [];
    public List<HouseholdMembership> Spaces { get; set; } = [];
    public int? ActiveLabelId { get; set; }
    public int? ActiveHouseholdId { get; set; }

    public async Task OnGetAsync([FromQuery] int? labelId, [FromQuery] int? householdId)
    {
        ActiveLabelId = labelId;
        ActiveHouseholdId = householdId;

        Spaces = await _householdMemberships.GetMembershipsAsync(User.Identity!.Name);
        var householdIds = Spaces.Select(membership => membership.HouseholdId).ToList();
        if (householdIds.Count == 0) return;

        if (householdId.HasValue && !householdIds.Contains(householdId.Value))
        {
            ActiveHouseholdId = null;
            householdId = null;
        }

        var selectedHouseholdIds = householdId.HasValue
            ? [householdId.Value]
            : householdIds;

        AllLabels = await _db.Labels
            .Where(l => selectedHouseholdIds.Contains(l.HouseholdId))
            .OrderBy(l => l.Name)
            .ToListAsync();

        var choresQuery = _db.Chores
            .Include(c => c.Labels)
            .Include(c => c.Household)
            .Where(c => selectedHouseholdIds.Contains(c.HouseholdId));

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
