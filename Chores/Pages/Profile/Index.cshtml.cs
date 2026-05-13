using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Profile;

[Authorize]
public class IndexModel(AppDbContext db) : PageModel
{
    public AppUser CurrentUser { get; set; } = null!;
    public List<FidoCredential> Passkeys { get; set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        var username = User.Identity!.Name!;
        var user = await db.Users
            .Include(u => u.Credentials)
            .FirstOrDefaultAsync(u => u.LoginName == username);

        if (user is null)
            return RedirectToPage("/Auth/Logout");

        CurrentUser = user;
        Passkeys = [.. user.Credentials.OrderBy(c => c.RegDate)];
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
            return Page();

        var credential = user.Credentials.FirstOrDefault(c => c.Id == credentialId && c.UserId == user.Id);
        if (credential is not null)
        {
            db.FidoCredentials.Remove(credential);
            await db.SaveChangesAsync();
        }

        return RedirectToPage();
    }
}
