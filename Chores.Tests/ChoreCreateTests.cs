using Chores.Data;
using Chores.Models;
using Chores.Pages.Chores;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chores.Tests;

public class ChoreCreateTests
{
    [Fact]
    public async Task OnGetAsync_PrefillsActiveLabelAndSelectsItsHousehold()
    {
        await using var db = CreateDbContext();
        var firstHousehold = new Household { Name = "A Space" };
        var filteredHousehold = new Household { Name = "B Space" };
        var user = new AppUser { LoginName = "alice" };
        var activeLabel = new Label
        {
            Name = "Blue",
            Color = "#0000ff",
            Household = filteredHousehold
        };

        db.Users.Add(user);
        db.Households.AddRange(firstHousehold, filteredHousehold);
        db.HouseholdMemberships.AddRange(
            new HouseholdMembership { User = user, Household = firstHousehold, IsOwner = true, JoinedAtUtc = DateTime.UtcNow },
            new HouseholdMembership { User = user, Household = filteredHousehold, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Labels.Add(activeLabel);
        await db.SaveChangesAsync();

        var model = CreateModel(db, user.LoginName);
        model.LabelId = activeLabel.Id;

        await model.OnGetAsync(null);

        Assert.Equal(filteredHousehold.Id, model.HouseholdId);
        Assert.Contains(activeLabel.Id, model.SelectedLabelIds);
        Assert.Contains(model.AvailableLabels, label => label.Id == activeLabel.Id);
    }

    [Fact]
    public async Task OnPostAsync_ReturnsPageErrorWhenUserHasNoSpaces()
    {
        await using var db = CreateDbContext();
        var user = new AppUser { LoginName = "alice" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = new CreateModel(db, new HouseholdMembershipService(db))
        {
            HouseholdId = 0,
            Name = "Dishes",
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, user.LoginName)
                    ],
                    "TestAuth"))
                }
            }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(model.Spaces);
        Assert.True(model.ModelState.ContainsKey(nameof(Chores.Pages.Chores.CreateModel.HouseholdId)));
    }

    [Fact]
    public void BuildManageChoresPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "alice");
        model.LabelId = 7;

        var path = model.BuildManageChoresPath();

        Assert.Equal("/chores/Chores?labelId=7", path);
    }

    private static CreateModel CreateModel(AppDbContext db, string loginName)
    {
        return new CreateModel(db, new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString("/chores")
                    },
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, loginName)
                    ],
                    "TestAuth"))
                }
            }
        };
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
