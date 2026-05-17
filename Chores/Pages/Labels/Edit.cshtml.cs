using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public EditModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    [BindProperty]
    public int LabelId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Color { get; set; } = "#6c757d";

    public string HouseholdName { get; set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var label = await _db.Labels
            .Include(l => l.Household)
            .FirstOrDefaultAsync(l => l.Id == id);
        if (label is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, label.HouseholdId)) return NotFound();

        LabelId = label.Id;
        Name = label.Name;
        Color = label.Color;
        HouseholdName = label.Household.Name;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        if (!LabelColorValidator.TryNormalize(Color, out var normalizedColor))
        {
            ModelState.AddModelError(nameof(Color), "Enter a valid hex color.");
            return Page();
        }

        var label = await _db.Labels.FirstOrDefaultAsync(l => l.Id == LabelId);
        if (label is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(User.Identity!.Name, label.HouseholdId)) return NotFound();

        label.Name = Name.Trim();
        label.Color = normalizedColor;
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
