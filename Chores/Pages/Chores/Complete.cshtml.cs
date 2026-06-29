using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Chores.Pages.Chores;

[Authorize]
public class CompleteModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ScheduleAdherenceService _adherence;
    private readonly HouseholdMembershipService _householdMemberships;

    public CompleteModel(
        AppDbContext db,
        ScheduleAdherenceService adherence,
        HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _adherence = adherence;
        _householdMemberships = householdMemberships;
    }

    public Chore Chore { get; set; } = null!;
    public DateTime? LastCompletedUtc { get; set; }
    public ScheduleAdherence? LastCompletionAdherence { get; set; }

    [BindProperty]
    public DateTime CompletedAt { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? LabelId { get; set; }

    [BindProperty(SupportsGet = true)]
    public List<int> HouseholdIds { get; set; } = [];

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var chore = await _db.Chores.FirstOrDefaultAsync(c => c.Id == id);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, chore.HouseholdId)) return NotFound();

        Chore = chore;
        CompletedAt = DateTime.Now;
        await LoadLastCompletionStatusAsync(chore);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        var completedAtUtc = DateTime.SpecifyKind(CompletedAt, DateTimeKind.Local).ToUniversalTime();
        return await SaveCompletionAsync(id, completedAtUtc);
    }

    public Task<IActionResult> OnPostYesterdayAsync(int id)
    {
        return SaveCompletionAsync(id, DateTime.UtcNow.AddHours(-24));
    }

    public string BuildDashboardPath()
    {
        var queryBuilder = new QueryBuilder();

        if (LabelId.HasValue)
        {
            queryBuilder.Add("labelId", LabelId.Value.ToString(CultureInfo.InvariantCulture));
        }

        if (!string.IsNullOrWhiteSpace(Sort))
        {
            queryBuilder.Add("sort", Sort);
        }

        foreach (var householdId in HouseholdIds)
        {
            queryBuilder.Add("householdIds", householdId.ToString(CultureInfo.InvariantCulture));
        }

        var queryString = queryBuilder.ToQueryString().Value;
        var pagePath = $"{Request.PathBase}/";
        return string.IsNullOrEmpty(queryString) ? pagePath : $"{pagePath}{queryString}";
    }

    private async Task<IActionResult> SaveCompletionAsync(int id, DateTime completedAtUtc)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var chore = await _db.Chores.FirstOrDefaultAsync(c => c.Id == id);
        if (chore is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(user.LoginName, chore.HouseholdId)) return NotFound();

        if (completedAtUtc > DateTime.UtcNow)
        {
            completedAtUtc = DateTime.UtcNow;
        }

        _db.CompletionRecords.Add(new CompletionRecord
        {
            ChoreId = chore.Id,
            CompletedByUserId = user.Id,
            CompletedAtUtc = completedAtUtc
        });

        await _db.SaveChangesAsync();
        return LocalRedirect(BuildDashboardPath());
    }

    private async Task LoadLastCompletionStatusAsync(Chore chore)
    {
        var lastCompletedUtc = await _db.CompletionRecords
            .Where(record => record.ChoreId == chore.Id)
            .OrderByDescending(record => record.CompletedAtUtc)
            .Select(record => (DateTime?)record.CompletedAtUtc)
            .FirstOrDefaultAsync();

        LastCompletedUtc = lastCompletedUtc;
        LastCompletionAdherence = _adherence.Evaluate(chore.Schedule, lastCompletedUtc);
    }
}
