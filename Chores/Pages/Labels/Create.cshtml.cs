using Chores.Data;
using Chores.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;

    public CreateModel(AppDbContext db) => _db = db;

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Color { get; set; } = "#6c757d";

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            return Page();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();

        _db.Labels.Add(new Label
        {
            Name = Name.Trim(),
            Color = Color,
            HouseholdId = user.HouseholdId
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }
}
