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

public enum DashboardSortMode
{
    Alphabet,
    Labels,
    NextDue
}

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
    public DashboardSortMode ActiveSortMode { get; set; } = DashboardSortMode.Alphabet;
    public bool IsAllSpacesSelected { get; set; }
    public bool ShowHouseholdNames => Spaces.Count > 1 && (IsAllSpacesSelected || ActiveHouseholdIds.Count > 1);
    public string SelectedSpacesSummary { get; set; } = "All spaces";
    public string ActiveSortModeLabel => ActiveSortMode switch
    {
        DashboardSortMode.Alphabet => "Alphabet",
        DashboardSortMode.Labels => "Labels",
        DashboardSortMode.NextDue => "Next due",
        _ => "Alphabet"
    };
    public bool HasCustomSortMode => ActiveSortMode != DashboardSortMode.Alphabet;
    public string ActiveSortQueryValue => ToQueryValue(ActiveSortMode);

    public async Task OnGetAsync([FromQuery] int? labelId, [FromQuery] List<int>? householdIds, [FromQuery] string? sort)
    {
        ActiveLabelId = labelId;
        ActiveSortMode = ParseSortMode(sort);

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

        var chores = await choresQuery.ToListAsync();

        var choreIds = chores.Select(c => c.Id).ToList();

        var latestRecords = await _db.CompletionRecords
            .Where(r => choreIds.Contains(r.ChoreId))
            .GroupBy(r => r.ChoreId)
            .Select(g => g.OrderByDescending(r => r.CompletedAtUtc).First())
            .ToListAsync();

        var lastByChore = latestRecords.ToDictionary(r => r.ChoreId);

        ChoreStatuses = SortChoreStatuses(chores.Select(c =>
        {
            lastByChore.TryGetValue(c.Id, out var last);
            var adherence = _adherence.Evaluate(c.Schedule, last?.CompletedAtUtc);
            return new ChoreStatus(c, adherence, last?.CompletedAtUtc);
        })).ToList();
    }

    // Keep dashboard links shareable by omitting redundant space filters when all spaces are selected.
    public string BuildDashboardPath(int? labelId = null, DashboardSortMode? sortMode = null)
    {
        var queryBuilder = new QueryBuilder();
        var effectiveSortMode = sortMode ?? ActiveSortMode;

        if (labelId.HasValue)
        {
            queryBuilder.Add("labelId", labelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (effectiveSortMode != DashboardSortMode.Alphabet)
        {
            queryBuilder.Add("sort", ToQueryValue(effectiveSortMode));
        }

        foreach (var householdId in ActiveHouseholdIds)
        {
            queryBuilder.Add("householdIds", householdId.ToString(CultureInfo.InvariantCulture));
        }

        var queryString = queryBuilder.ToQueryString().Value;
        var pagePath = $"{Request.PathBase}/";
        return string.IsNullOrEmpty(queryString) ? pagePath : $"{pagePath}{queryString}";
    }

    public string BuildCreateChorePath()
    {
        var queryBuilder = new QueryBuilder();

        if (EffectiveHouseholdIds.Count == 1)
        {
            queryBuilder.Add("householdId", EffectiveHouseholdIds[0].ToString(CultureInfo.InvariantCulture));
        }

        var pagePath = $"{Request.PathBase}/Chores/Create";
        var queryString = queryBuilder.ToQueryString().Value;
        return string.IsNullOrEmpty(queryString) ? pagePath : $"{pagePath}{queryString}";
    }

    public string BuildCompletePath(int choreId)
    {
        var queryBuilder = new QueryBuilder
        {
            { "id", choreId.ToString(CultureInfo.InvariantCulture) }
        };

        if (ActiveLabelId.HasValue)
        {
            queryBuilder.Add("labelId", ActiveLabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (ActiveSortMode != DashboardSortMode.Alphabet)
        {
            queryBuilder.Add("sort", ToQueryValue(ActiveSortMode));
        }

        foreach (var householdId in ActiveHouseholdIds)
        {
            queryBuilder.Add("householdIds", householdId.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/Complete{queryBuilder.ToQueryString().Value}";
    }

    public DashboardSortMode GetNextSortMode()
    {
        return ActiveSortMode switch
        {
            DashboardSortMode.Alphabet => DashboardSortMode.Labels,
            DashboardSortMode.Labels => DashboardSortMode.NextDue,
            DashboardSortMode.NextDue => DashboardSortMode.Alphabet,
            _ => DashboardSortMode.Alphabet
        };
    }

    private IEnumerable<ChoreStatus> SortChoreStatuses(IEnumerable<ChoreStatus> choreStatuses)
    {
        return ActiveSortMode switch
        {
            DashboardSortMode.Labels => choreStatuses
                .OrderBy(choreStatus => choreStatus.Chore.Labels.Count == 0 ? 1 : 0)
                .ThenBy(choreStatus => GetPrimaryLabelName(choreStatus.Chore), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(choreStatus => choreStatus.Chore.Name, StringComparer.CurrentCultureIgnoreCase),
            DashboardSortMode.NextDue => choreStatuses
                .OrderBy(choreStatus => GetDueSortValue(choreStatus.Adherence))
                .ThenBy(choreStatus => choreStatus.Chore.Name, StringComparer.CurrentCultureIgnoreCase),
            _ => choreStatuses
                .OrderBy(choreStatus => choreStatus.Chore.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(choreStatus => choreStatus.Chore.Household.Name, StringComparer.CurrentCultureIgnoreCase)
        };
    }

    private static string GetPrimaryLabelName(Chore chore)
    {
        return chore.Labels
            .OrderBy(label => label.Name, StringComparer.CurrentCultureIgnoreCase)
            .Select(label => label.Name)
            .FirstOrDefault() ?? "~";
    }

    private static int GetDueSortValue(ScheduleAdherence adherence)
    {
        return adherence.Status switch
        {
            AdherenceStatus.Overdue when adherence.DaysOverdue == int.MaxValue => int.MinValue,
            AdherenceStatus.Overdue => -adherence.DaysOverdue,
            _ when adherence.DaysUntilDue.HasValue => adherence.DaysUntilDue.Value,
            _ => int.MaxValue
        };
    }

    private static DashboardSortMode ParseSortMode(string? sort)
    {
        return sort?.Trim().ToLowerInvariant() switch
        {
            "labels" => DashboardSortMode.Labels,
            "due" => DashboardSortMode.NextDue,
            _ => DashboardSortMode.Alphabet
        };
    }

    private static string ToQueryValue(DashboardSortMode sortMode)
    {
        return sortMode switch
        {
            DashboardSortMode.Labels => "labels",
            DashboardSortMode.NextDue => "due",
            _ => "alphabet"
        };
    }

    private void SetSpaceSelection(IReadOnlyCollection<int> availableHouseholdIds, IReadOnlyCollection<int> requestedHouseholdIds)
    {
        var selection = BuildSpaceSelection(availableHouseholdIds, requestedHouseholdIds);
        IsAllSpacesSelected = selection.IsAllSpacesSelected;
        EffectiveHouseholdIds = selection.EffectiveHouseholdIds;
        ActiveHouseholdIds = selection.ActiveHouseholdIds;

        if (IsAllSpacesSelected)
        {
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

    private static SpaceSelection BuildSpaceSelection(
        IReadOnlyCollection<int> availableHouseholdIds,
        IReadOnlyCollection<int> requestedHouseholdIds)
    {
        var requestedAccessibleHouseholdIds = requestedHouseholdIds
            .Where(availableHouseholdIds.Contains)
            .Distinct()
            .ToList();

        var isAllSpacesSelected = requestedAccessibleHouseholdIds.Count is 0
            || requestedAccessibleHouseholdIds.Count == availableHouseholdIds.Count;

        List<int> effectiveHouseholdIds = isAllSpacesSelected
            ? [.. availableHouseholdIds]
            : [.. requestedAccessibleHouseholdIds];

        // An empty active list means "all spaces" and keeps dashboard links free of
        // redundant householdIds filters.
        List<int> activeHouseholdIds = isAllSpacesSelected
            ? []
            : [.. requestedAccessibleHouseholdIds];

        return new SpaceSelection(isAllSpacesSelected, effectiveHouseholdIds, activeHouseholdIds);
    }

    private record SpaceSelection(
        bool IsAllSpacesSelected,
        List<int> EffectiveHouseholdIds,
        List<int> ActiveHouseholdIds);

    public record ChoreStatus(Chore Chore, ScheduleAdherence Adherence, DateTime? LastCompletedUtc);
}
