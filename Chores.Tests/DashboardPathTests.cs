using Chores.Data;
using Chores.Models;
using Chores.Pages;
using Chores.Pages.Chores;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chores.Tests;

public class DashboardPathTests
{
    [Fact]
    public void BuildDashboardPath_IncludesPathBase()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.ActiveHouseholdIds = [2, 3];

        var path = model.BuildDashboardPath(labelId: 7);

        Assert.Equal("/chores/?labelId=7&householdIds=2&householdIds=3", path);
    }

    [Fact]
    public void BuildDashboardPath_IncludesCustomSortMode()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.ActiveSortMode = DashboardSortMode.NextDue;

        var path = model.BuildDashboardPath();

        Assert.Equal("/chores/?sort=due", path);
    }

    [Fact]
    public void BuildCreateChorePath_IncludesPathBase()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.EffectiveHouseholdIds = [4];

        var path = model.BuildCreateChorePath();

        Assert.Equal("/chores/Chores/Create?householdId=4", path);
    }

    [Fact]
    public void BuildCompletePath_PreservesActiveFilters()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.ActiveLabelId = 7;
        model.ActiveHouseholdIds = [2, 3];
        model.ActiveSortMode = DashboardSortMode.Labels;

        var path = model.BuildCompletePath(11);

        Assert.Equal("/chores/Chores/Complete?id=11&labelId=7&sort=labels&householdIds=2&householdIds=3", path);
    }

    [Fact]
    public void CompleteBuildDashboardPath_PreservesActiveFilters()
    {
        using var db = CreateDbContext();
        var model = new CompleteModel(db, new HouseholdMembershipService(db))
        {
            LabelId = 7,
            Sort = "due",
            HouseholdIds = [2, 3],
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString("/chores")
                    }
                }
            }
        };

        var path = model.BuildDashboardPath();

        Assert.Equal("/chores/?labelId=7&sort=due&householdIds=2&householdIds=3", path);
    }

    [Fact]
    public async Task OnGetAsync_SortsChoresByLabelWhenRequested()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var alphaLabel = new Label { Name = "Alpha", Color = "#111111", Household = household };
        var betaLabel = new Label { Name = "Beta", Color = "#222222", Household = household };
        var unlabeledChore = new Chore { Name = "Laundry", Household = household, Schedule = Schedule.Weekly };
        var betaChore = new Chore { Name = "Bathroom", Household = household, Schedule = Schedule.Weekly, Labels = [betaLabel] };
        var alphaChore = new Chore { Name = "Dishes", Household = household, Schedule = Schedule.Weekly, Labels = [alphaLabel] };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.AddRange(unlabeledChore, betaChore, alphaChore);
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, "/chores", user.LoginName);

        await model.OnGetAsync(null, null, "labels");

        Assert.Equal(["Dishes", "Bathroom", "Laundry"], model.ChoreStatuses.Select(status => status.Chore.Name).ToArray());
    }

    [Fact]
    public async Task OnGetAsync_SortsChoresByNextDueWhenRequested()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var overdueChore = new Chore { Name = "Overdue", Household = household, Schedule = Schedule.Daily };
        var dueTodayChore = new Chore { Name = "Due Today", Household = household, Schedule = Schedule.Daily };
        var onTimeChore = new Chore { Name = "On Time", Household = household, Schedule = Schedule.Daily };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.AddRange(overdueChore, dueTodayChore, onTimeChore);
        await db.SaveChangesAsync();

        db.CompletionRecords.AddRange(
            new CompletionRecord { ChoreId = overdueChore.Id, CompletedByUserId = user.Id, CompletedAtUtc = DateTime.UtcNow.AddDays(-3) },
            new CompletionRecord { ChoreId = dueTodayChore.Id, CompletedByUserId = user.Id, CompletedAtUtc = DateTime.UtcNow.AddDays(-1) },
            new CompletionRecord { ChoreId = onTimeChore.Id, CompletedByUserId = user.Id, CompletedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, "/chores", user.LoginName);

        await model.OnGetAsync(null, null, "due");

        Assert.Equal(["Overdue", "Due Today", "On Time"], model.ChoreStatuses.Select(status => status.Chore.Name).ToArray());
    }

    [Fact]
    public void ManageChoresBuildPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = CreateChoresIndexModel(db, "/chores");

        var path = model.BuildManageChoresPath(7);

        Assert.Equal("/chores/Chores?labelId=7", path);
    }

    [Fact]
    public void ManageChoresBuildEditPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = CreateChoresIndexModel(db, "/chores");
        model.ActiveLabelId = 7;

        var path = model.BuildEditPath(11);

        Assert.Equal("/chores/Chores/Edit?id=11&labelId=7", path);
    }

    [Fact]
    public void ManageChoresBuildHistoryPath_UsesRouteSegmentAndPreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = CreateChoresIndexModel(db, "/chores");
        model.ActiveLabelId = 7;

        var path = model.BuildHistoryPath(11);

        Assert.Equal("/chores/Chores/History/11?labelId=7", path);
    }

    [Fact]
    public void ManageChoresBuildCreatePath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = CreateChoresIndexModel(db, "/chores");
        model.ActiveLabelId = 7;

        var path = model.BuildCreatePath();

        Assert.Equal("/chores/Chores/Create?labelId=7", path);
    }

    [Fact]
    public void EditBuildManageChoresPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = new EditModel(db, new HouseholdMembershipService(db))
        {
            LabelId = 7,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString("/chores")
                    }
                }
            }
        };

        var path = model.BuildManageChoresPath();

        Assert.Equal("/chores/Chores?labelId=7", path);
    }

    [Fact]
    public void CreateBuildManageChoresPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = new CreateModel(db, new HouseholdMembershipService(db))
        {
            LabelId = 7,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString("/chores")
                    }
                }
            }
        };

        var path = model.BuildManageChoresPath();

        Assert.Equal("/chores/Chores?labelId=7", path);
    }

    [Fact]
    public void HistoryBuildManageChoresPath_PreservesActiveLabel()
    {
        using var db = CreateDbContext();
        var model = new HistoryModel(db, new ScheduleAdherenceService(), new HouseholdMembershipService(db))
        {
            LabelId = 7,
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString("/chores")
                    }
                }
            }
        };

        var path = model.BuildManageChoresPath();

        Assert.Equal("/chores/Chores?labelId=7", path);
    }

    private static Chores.Pages.IndexModel CreateModel(AppDbContext db, string pathBase)
    {
        return new Chores.Pages.IndexModel(db, new ScheduleAdherenceService(), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString(pathBase)
                    }
                }
            }
        };
    }

    private static Chores.Pages.IndexModel CreateAuthenticatedModel(AppDbContext db, string pathBase, string loginName)
    {
        return new Chores.Pages.IndexModel(db, new ScheduleAdherenceService(), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString(pathBase)
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

    private static Chores.Pages.Chores.IndexModel CreateChoresIndexModel(AppDbContext db, string pathBase)
    {
        return new Chores.Pages.Chores.IndexModel(db, new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    Request =
                    {
                        PathBase = new PathString(pathBase)
                    }
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
