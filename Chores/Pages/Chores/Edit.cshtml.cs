using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class EditModel : PageModel
{
    private readonly AppDbContext _db;

    public EditModel(AppDbContext db) => _db = db;

    [BindProperty]
    public int ChoreId { get; set; }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public Schedule Schedule { get; set; }

    [BindProperty]
    public List<int> SelectedLabelIds { get; set; } = [];

    public List<Label> AvailableLabels { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var chore = await _db.Chores
            .Include(c => c.Labels)
            .FirstOrDefaultAsync(c => c.Id == id && c.HouseholdId == user.HouseholdId);
        if (chore is null) return NotFound();

        AvailableLabels = await _db.Labels
            .Where(l => l.HouseholdId == user.HouseholdId)
            .OrderBy(l => l.Name)
            .ToListAsync();

        ChoreId = chore.Id;
        Name = chore.Name;
        Schedule = chore.Schedule;
        SelectedLabelIds = chore.Labels.Select(l => l.Id).ToList();
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

        var chore = await _db.Chores
            .Include(c => c.Labels)
            .FirstOrDefaultAsync(c => c.Id == ChoreId && c.HouseholdId == user.HouseholdId);
        if (chore is null) return NotFound();

        chore.Name = Name.Trim();
        chore.Schedule = Schedule;
        chore.Labels.Clear();

        if (SelectedLabelIds.Count > 0)
        {
            var labels = await _db.Labels
                .Where(l => SelectedLabelIds.Contains(l.Id) && l.HouseholdId == user.HouseholdId)
                .ToListAsync();
            foreach (var label in labels)
                chore.Labels.Add(label);
        }

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
