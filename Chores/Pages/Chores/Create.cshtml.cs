using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Chores;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;

    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public Schedule Schedule { get; set; } = Schedule.Weekly;

    [BindProperty]
    public List<int> SelectedLabelIds { get; set; } = [];

    public List<Label> AvailableLabels { get; set; } = [];

    public async Task OnGetAsync()
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return;
        AvailableLabels = await _db.Labels
            .Where(l => l.HouseholdId == user.HouseholdId)
            .OrderBy(l => l.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        var chore = new Chore
        {
            Name = Name.Trim(),
            Schedule = Schedule,
            HouseholdId = user.HouseholdId
        };

        if (SelectedLabelIds.Count > 0)
        {
            var labels = await _db.Labels
                .Where(l => SelectedLabelIds.Contains(l.Id) && l.HouseholdId == user.HouseholdId)
                .ToListAsync();
            foreach (var label in labels)
                chore.Labels.Add(label);
        }

        _db.Chores.Add(chore);
        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
