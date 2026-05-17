using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace Chores.Pages.Profile;

[Authorize]
public class IndexModel(
    AppDbContext db,
    HouseholdInvitationService householdInvitations,
    HouseholdMembershipService householdMemberships) : PageModel
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppUser CurrentUser { get; set; } = null!;
    public List<FidoCredential> Passkeys { get; set; } = [];
    public List<HouseholdInvite> PendingInvites { get; set; } = [];
    public List<HouseholdMembership> Spaces { get; set; } = [];
    public bool CanAcceptHouseholdInvites { get; set; }
    [BindProperty]
    public int? ExportHouseholdId { get; set; }
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
            return RedirectToPage("/Auth/Logout");

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync([FromQuery] int? householdId = null)
    {
        return await ExportAsync(householdId);
    }

    public async Task<IActionResult> OnPostExportAsync()
    {
        return await ExportAsync(ExportHouseholdId);
    }

    private async Task<IActionResult> ExportAsync(int? householdId)
    {
        var exportedAtUtc = DateTime.UtcNow;
        var username = User.Identity!.Name!;
        var user = await householdMemberships.GetUserAsync(username);
        var membership = await GetExportMembershipAsync(username, householdId);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        if (membership is null)
            return await ShowMissingExportSpaceAsync();

        var labels = await db.Labels
            .AsNoTracking()
            .Where(label => label.HouseholdId == membership.HouseholdId)
            .OrderBy(label => label.Name)
            .Select(label => new ExportLabel(label.Id, label.Name, label.Color))
            .ToListAsync();

        var chores = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == membership.HouseholdId)
            .Include(chore => chore.Labels)
            .OrderBy(chore => chore.Name)
            .ToListAsync();

        var completionHistoryByChoreId = await LoadCompletionHistoryByChoreIdAsync([.. chores.Select(chore => chore.Id)]);

        var export = new ExportPayload(
            1,
            exportedAtUtc,
            user.LoginName,
            new ExportHousehold(membership.HouseholdId, membership.Household.Name),
            [.. labels],
            [.. chores.Select(chore => new ExportChore(
                chore.Id,
                chore.Name,
                chore.Schedule.ToString(),
                [.. chore.Labels
                    .OrderBy(label => label.Name)
                    .Select(label => new ExportLabel(label.Id, label.Name, label.Color))],
                completionHistoryByChoreId.GetValueOrDefault(chore.Id, [])))]);

        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"chores-export-{exportedAtUtc:yyyyMMddHHmmss}.json";

        return File(bytes, "application/json; charset=utf-8", fileName);
    }

    public async Task<IActionResult> OnGetChoreListExportAsync([FromQuery] int? householdId = null)
    {
        return await ExportChoreListAsync(householdId);
    }

    public async Task<IActionResult> OnPostChoreListExportAsync()
    {
        return await ExportChoreListAsync(ExportHouseholdId);
    }

    private async Task<IActionResult> ExportChoreListAsync(int? householdId)
    {
        var exportedAtUtc = DateTime.UtcNow;
        var username = User.Identity!.Name!;
        var user = await householdMemberships.GetUserAsync(username);
        var membership = await GetExportMembershipAsync(username, householdId);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        if (membership is null)
            return await ShowMissingExportSpaceAsync();

        var chores = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == membership.HouseholdId)
            .OrderBy(chore => chore.Name)
            .Select(chore => new MinimalExportChore(chore.Name, chore.Schedule.ToString()))
            .ToListAsync();

        var export = new MinimalExportPayload(
            1,
            exportedAtUtc,
            user.LoginName,
            [.. chores]);

        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"chores-list-export-{exportedAtUtc:yyyyMMddHHmmss}.json";

        return File(bytes, "application/json; charset=utf-8", fileName);
    }

    public async Task<IActionResult> OnPostRenameAsync(int credentialId, string? nickname)
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.LoginName == username);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        var credential = user.Credentials.FirstOrDefault(c => c.Id == credentialId && c.UserId == user.Id);
        if (credential is not null)
        {
            credential.Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim();
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int credentialId)
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.LoginName == username);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        if (user.Credentials.Count <= 1)
        {
            StatusMessage = "You must keep at least one passkey.";
            await LoadAsync();
            return Page();
        }

        var credential = user.Credentials.FirstOrDefault(c => c.Id == credentialId && c.UserId == user.Id);
        if (credential is not null)
        {
            db.FidoCredentials.Remove(credential);
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAcceptInviteAsync(int inviteId)
    {
        var username = User.Identity!.Name!;
        var user = await db.Users.FirstOrDefaultAsync(candidate => candidate.LoginName == username);
        if (user is null)
            return RedirectToPage("/Auth/Logout");

        if (!await householdInvitations.AcceptInviteAsync(user, inviteId))
        {
            StatusMessage = "That invite is no longer available.";
            await LoadAsync();
            return Page();
        }

        StatusMessage = "Invite accepted.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeclineInviteAsync(int inviteId)
    {
        var username = User.Identity!.Name!;
        if (!await householdInvitations.DeclineInviteAsync(username, inviteId))
        {
            StatusMessage = "That invite is no longer available.";
            await LoadAsync();
            return Page();
        }

        StatusMessage = "Invite declined.";
        return RedirectToPage();
    }

    private async Task<bool> LoadAsync()
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.LoginName == username);

        if (user is null)
            return false;

        CurrentUser = user;
        Passkeys = [.. user.Credentials.OrderBy(c => c.RegDate)];
        PendingInvites = await householdInvitations.GetPendingInvitesAsync(user);
        CanAcceptHouseholdInvites = await householdInvitations.CanAcceptInvitesAsync(user);
        Spaces = await householdMemberships.GetMembershipsAsync(username);
        ExportHouseholdId ??= Spaces.FirstOrDefault()?.HouseholdId;
        return true;
    }

    private async Task<HouseholdMembership?> GetExportMembershipAsync(string username, int? householdId)
    {
        return householdId.HasValue
            ? await householdMemberships.GetMembershipAsync(username, householdId.Value)
            : await householdMemberships.GetDefaultMembershipAsync(username);
    }

    private async Task<IActionResult> ShowMissingExportSpaceAsync()
    {
        StatusMessage = "Select a space you can access before exporting.";
        await LoadAsync();
        return Page();
    }

    private async Task<Dictionary<int, IReadOnlyList<ExportCompletion>>> LoadCompletionHistoryByChoreIdAsync(IReadOnlyList<int> choreIds)
    {
        if (choreIds.Count == 0)
            return [];

        var completionRows = await db.CompletionRecords
            .AsNoTracking()
            .Where(record => choreIds.Contains(record.ChoreId))
            .OrderBy(record => record.ChoreId)
            .ThenBy(record => record.CompletedAtUtc)
            .Select(record => new
            {
                record.ChoreId,
                Completion = new ExportCompletion(
                    record.Id,
                    record.CompletedAtUtc,
                    record.CompletedByUserId,
                    record.CompletedByUser.LoginName)
            })
            .ToListAsync();

        return completionRows
            .GroupBy(row => row.ChoreId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ExportCompletion>)[.. group.Select(row => row.Completion)]);
    }

    private sealed record ExportPayload(
        int Version,
        DateTime ExportedAtUtc,
        string ExportedByLoginName,
        ExportHousehold Household,
        IReadOnlyList<ExportLabel> Labels,
        IReadOnlyList<ExportChore> Chores);

    private sealed record ExportHousehold(int Id, string Name);

    private sealed record ExportLabel(int Id, string Name, string Color);

    private sealed record ExportChore(
        int Id,
        string Name,
        string Schedule,
        IReadOnlyList<ExportLabel> Labels,
        IReadOnlyList<ExportCompletion> CompletionHistory);

    private sealed record ExportCompletion(
        int Id,
        DateTime CompletedAtUtc,
        int CompletedByUserId,
        string CompletedByLoginName);

    private sealed record MinimalExportPayload(
        int Version,
        DateTime ExportedAtUtc,
        string ExportedByLoginName,
        IReadOnlyList<MinimalExportChore> Chores);

    private sealed record MinimalExportChore(string Name, string Schedule);
}
