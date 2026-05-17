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
public class IndexModel(AppDbContext db, HouseholdInvitationService householdInvitations) : PageModel
{
    private static readonly JsonSerializerOptions ExportJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppUser CurrentUser { get; set; } = null!;
    public List<FidoCredential> Passkeys { get; set; } = [];
    public List<HouseholdInvite> PendingInvites { get; set; } = [];
    [TempData]
    public string? StatusMessage { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        if (!await LoadAsync())
            return RedirectToPage("/Auth/Logout");

        return Page();
    }

    public async Task<IActionResult> OnGetExportAsync()
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .AsNoTracking()
            .Include(candidate => candidate.Household)
            .FirstOrDefaultAsync(candidate => candidate.LoginName == username);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        var labels = await db.Labels
            .AsNoTracking()
            .Where(label => label.HouseholdId == user.HouseholdId)
            .OrderBy(label => label.Name)
            .Select(label => new ExportLabel(label.Id, label.Name, label.Color))
            .ToListAsync();

        var chores = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == user.HouseholdId)
            .Include(chore => chore.Labels)
            .Include(chore => chore.CompletionRecords)
                .ThenInclude(record => record.CompletedByUser)
            .OrderBy(chore => chore.Name)
            .ToListAsync();

        var export = new ExportPayload(
            1,
            DateTime.UtcNow,
            user.LoginName,
            new ExportHousehold(user.HouseholdId, user.Household.Name),
            [.. labels],
            [.. chores.Select(chore => new ExportChore(
                chore.Id,
                chore.Name,
                chore.Schedule.ToString(),
                [.. chore.Labels
                    .OrderBy(label => label.Name)
                    .Select(label => new ExportLabel(label.Id, label.Name, label.Color))],
                [.. chore.CompletionRecords
                    .OrderBy(record => record.CompletedAtUtc)
                    .Select(record => new ExportCompletion(
                        record.Id,
                        record.CompletedAtUtc,
                        record.CompletedByUserId,
                        record.CompletedByUser.LoginName))]))]);

        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"chores-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

        return File(bytes, "application/json; charset=utf-8", fileName);
    }

    public async Task<IActionResult> OnGetChoreListExportAsync()
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LoginName == username);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        var chores = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == user.HouseholdId)
            .OrderBy(chore => chore.Name)
            .Select(chore => new MinimalExportChore(chore.Name, chore.Schedule.ToString()))
            .ToListAsync();

        var export = new MinimalExportPayload(
            1,
            DateTime.UtcNow,
            user.LoginName,
            [.. chores]);

        var json = JsonSerializer.Serialize(export, ExportJsonOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        var fileName = $"chores-list-export-{DateTime.UtcNow:yyyyMMddHHmmss}.json";

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
            StatusMessage = user.IsHouseholdOwner
                ? "Transfer household ownership before joining another household."
                : "That invite is no longer available.";
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
        return true;
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
