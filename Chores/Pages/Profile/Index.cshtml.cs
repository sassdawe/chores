using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Profile;

[Authorize]
public class IndexModel(AppDbContext db, HouseholdInvitationService householdInvitations) : PageModel
{
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
}
