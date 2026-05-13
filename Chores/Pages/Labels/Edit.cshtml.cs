using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    public EditModel(AppDbContext db) => _db = db;

    [BindProperty]
    public int LabelId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Color { get; set; } = "#6c757d";

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var label = await _db.Labels.FirstOrDefaultAsync(l => l.Id == id && l.HouseholdId == user.HouseholdId);
        if (label is null) return NotFound();

        LabelId = label.Id;
        Name = label.Name;
        Color = label.Color;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var label = await _db.Labels.FirstOrDefaultAsync(l => l.Id == LabelId && l.HouseholdId == user.HouseholdId);
        if (label is null) return NotFound();

        label.Name = Name.Trim();
        label.Color = Color;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
