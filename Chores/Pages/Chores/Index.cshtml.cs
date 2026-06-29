using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Chores.Pages.Chores;

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

    public List<Chore> Chores { get; set; } = [];
    public List<Label> AllLabels { get; set; } = [];
    public int? ActiveLabelId { get; set; }
    public int TotalChoreCount { get; set; }
    public int OnTimeChoreCount { get; set; }
    public int NeverDoneChoreCount { get; set; }
    public int MissedTargetChoreCount { get; set; }

    public async Task OnGetAsync(int? labelId)
    {
        ActiveLabelId = labelId;

        var householdIds = await _householdMemberships.GetHouseholdIdsAsync(User.Identity!.Name);
        if (householdIds.Count == 0) return;

        AllLabels = await _db.Labels
            .Where(label => householdIds.Contains(label.HouseholdId))
            .OrderBy(label => label.Name)
            .ToListAsync();

        var choresQuery = _db.Chores
            .Include(c => c.Labels)
            .Include(c => c.Household)
            .Where(c => householdIds.Contains(c.HouseholdId));

        if (labelId.HasValue)
        {
            choresQuery = choresQuery.Where(chore => chore.Labels.Any(label => label.Id == labelId.Value));
        }

        Chores = await choresQuery
            .OrderBy(c => c.Household.Name)
            .ThenBy(c => c.Name)
            .ToListAsync();

        TotalChoreCount = Chores.Count;
        if (Chores.Count == 0)
        {
            return;
        }

        var choreIds = Chores.Select(chore => chore.Id).ToList();
        var latestRecords = await _db.CompletionRecords
            .Where(record => choreIds.Contains(record.ChoreId))
            .GroupBy(record => record.ChoreId)
            .Select(group => group.OrderByDescending(record => record.CompletedAtUtc).First())
            .ToListAsync();

        var lastByChore = latestRecords.ToDictionary(record => record.ChoreId);
        var adherences = Chores
            .Select(chore =>
            {
                lastByChore.TryGetValue(chore.Id, out var lastRecord);
                return _adherence.Evaluate(chore.Schedule, lastRecord?.CompletedAtUtc);
            })
            .ToList();

        NeverDoneChoreCount = adherences.Count(adherence =>
            adherence.Status == AdherenceStatus.Overdue
            && adherence.DaysOverdue == int.MaxValue);

        MissedTargetChoreCount = adherences.Count(adherence =>
            adherence.Status == AdherenceStatus.Overdue
            && adherence.DaysOverdue != int.MaxValue);

        OnTimeChoreCount = TotalChoreCount - NeverDoneChoreCount - MissedTargetChoreCount;
    }

    public string BuildManageChoresPath(int? labelId = null)
    {
        var queryBuilder = new QueryBuilder();

        if (labelId.HasValue)
        {
            queryBuilder.Add("labelId", labelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        var queryString = queryBuilder.ToQueryString().Value;
        var pagePath = $"{Request.PathBase}/Chores";
        return string.IsNullOrEmpty(queryString) ? pagePath : $"{pagePath}{queryString}";
    }

    public string BuildEditPath(int choreId)
    {
        return BuildChoreActionPath("Edit", choreId);
    }

    public string BuildCreatePath()
    {
        var queryBuilder = new QueryBuilder();

        if (ActiveLabelId.HasValue)
        {
            queryBuilder.Add("labelId", ActiveLabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/Create{queryBuilder.ToQueryString().Value}";
    }

    public string BuildHistoryPath(int choreId)
    {
        var queryBuilder = new QueryBuilder();

        if (ActiveLabelId.HasValue)
        {
            queryBuilder.Add("labelId", ActiveLabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/History/{choreId}{queryBuilder.ToQueryString().Value}";
    }

    private string BuildChoreActionPath(string pageName, int choreId)
    {
        var queryBuilder = new QueryBuilder
        {
            { "id", choreId.ToString(CultureInfo.InvariantCulture) }
        };

        if (ActiveLabelId.HasValue)
        {
            queryBuilder.Add("labelId", ActiveLabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        return $"{Request.PathBase}/Chores/{pageName}{queryBuilder.ToQueryString().Value}";
    }
}
