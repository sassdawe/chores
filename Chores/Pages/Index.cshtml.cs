using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

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
    public List<int> ActiveHouseholdIds { get; set; } = [];
    public List<int> EffectiveHouseholdIds { get; set; } = [];
    public bool IsAllSpacesSelected { get; set; }
    public bool ShowHouseholdNames => Spaces.Count > 1 && EffectiveHouseholdIds.Count != 1;
    public string SelectedSpacesSummary { get; set; } = "All spaces";

    public async Task OnGetAsync([FromQuery] int? labelId, [FromQuery] List<int>? householdIds)
    {
        ActiveLabelId = labelId;

        Spaces = await _householdMemberships.GetMembershipsAsync(User.Identity!.Name);
        var availableHouseholdIds = Spaces.Select(membership => membership.HouseholdId).ToList();
        if (availableHouseholdIds.Count == 0)
        {
            return;
        }

        SetSpaceSelection(availableHouseholdIds, householdIds ?? []);

        AllLabels = await _db.Labels
            .Where(label => EffectiveHouseholdIds.Contains(label.HouseholdId))
            .OrderBy(label => label.Name)
            .ToListAsync();

        var choresQuery = _db.Chores
            .Include(chore => chore.Labels)
            .Include(chore => chore.Household)
            .Where(chore => EffectiveHouseholdIds.Contains(chore.HouseholdId));

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

    // Keep dashboard links shareable by omitting redundant space filters when all spaces are selected.
    public string BuildDashboardPath(int? labelId = null)
    {
        var queryBuilder = new QueryBuilder();

        if (labelId.HasValue)
        {
            queryBuilder.Add("labelId", labelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        foreach (var householdId in ActiveHouseholdIds)
        {
            queryBuilder.Add("householdIds", householdId.ToString(CultureInfo.InvariantCulture));
        }

        var queryString = queryBuilder.ToQueryString().Value;
        return string.IsNullOrEmpty(queryString) ? "/" : $"/{queryString}";
    }

    public string BuildCreateChorePath()
    {
        var queryBuilder = new QueryBuilder();

        if (EffectiveHouseholdIds.Count == 1)
        {
            queryBuilder.Add("householdId", EffectiveHouseholdIds[0].ToString(CultureInfo.InvariantCulture));
        }

        var queryString = queryBuilder.ToQueryString().Value;
        return string.IsNullOrEmpty(queryString) ? "/Chores/Create" : $"/Chores/Create{queryString}";
    }

    private void SetSpaceSelection(IReadOnlyCollection<int> availableHouseholdIds, IReadOnlyCollection<int> requestedHouseholdIds)
    {
        ActiveHouseholdIds = requestedHouseholdIds
            .Where(availableHouseholdIds.Contains)
            .Distinct()
            .ToList();

        IsAllSpacesSelected = ActiveHouseholdIds.Count is 0 || ActiveHouseholdIds.Count == availableHouseholdIds.Count;
        EffectiveHouseholdIds = IsAllSpacesSelected
            ? [.. availableHouseholdIds]
            : [.. ActiveHouseholdIds];

        if (IsAllSpacesSelected)
        {
            ActiveHouseholdIds = [];
            SelectedSpacesSummary = "All spaces";
            return;
        }

        var selectedNames = Spaces
            .Where(space => EffectiveHouseholdIds.Contains(space.HouseholdId))
            .Select(space => space.Household.Name)
            .ToList();

        SelectedSpacesSummary = selectedNames.Count switch
        {
            0 => "All spaces",
            1 => selectedNames[0],
            2 => string.Join(", ", selectedNames),
            _ => $"{selectedNames.Count} spaces selected"
        };
    }

    public record ChoreStatus(Chore Chore, ScheduleAdherence Adherence, DateTime? LastCompletedUtc);
}
