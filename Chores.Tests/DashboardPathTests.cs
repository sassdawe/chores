using Chores.Data;
using Chores.Pages;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

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
    public void BuildCreateChorePath_IncludesPathBase()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.EffectiveHouseholdIds = [4];

        var path = model.BuildCreateChorePath();

        Assert.Equal("/chores/Chores/Create?householdId=4", path);
    }

    private static IndexModel CreateModel(AppDbContext db, string pathBase)
    {
        return new IndexModel(db, new ScheduleAdherenceService(), new HouseholdMembershipService(db))
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
