using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Chores.Pages.Labels;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public CreateModel(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    [BindProperty]
    public string Name { get; set; } = string.Empty;

    [BindProperty]
    public string Color { get; set; } = "#6c757d";

    [BindProperty]
    public int HouseholdId { get; set; }

    public List<HouseholdMembership> Spaces { get; set; } = [];

    public async Task OnGetAsync([FromQuery] int? householdId)
    {
        await LoadSpacesAsync(householdId);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ModelState.AddModelError(nameof(Name), "Name is required.");
            await LoadSpacesAsync(HouseholdId);
            return Page();
        }

        if (!LabelColorValidator.TryNormalize(Color, out var normalizedColor))
        {
            ModelState.AddModelError(nameof(Color), "Enter a valid hex color.");
            await LoadSpacesAsync(HouseholdId);
            return Page();
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == User.Identity!.Name);
        if (user is null) return NotFound();
        if (!await _householdMemberships.CanAccessHouseholdAsync(user.LoginName, HouseholdId)) return NotFound();

        _db.Labels.Add(new Label
        {
            Name = Name.Trim(),
            Color = normalizedColor,
            HouseholdId = HouseholdId
        });

        await _db.SaveChangesAsync();
        return RedirectToPage("Index");
    }

    private async Task LoadSpacesAsync(int? householdId = null)
    {
        Spaces = await _householdMemberships.GetMembershipsAsync(User.Identity!.Name);
        HouseholdId = householdId.HasValue && Spaces.Any(space => space.HouseholdId == householdId.Value)
            ? householdId.Value
            : Spaces.FirstOrDefault()?.HouseholdId ?? 0;
    }
}
