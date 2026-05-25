using Chores.Data;
using Chores.Pages;
using Chores.Pages.Chores;
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

    [Fact]
    public void BuildCompletePath_PreservesActiveFilters()
    {
        using var db = CreateDbContext();
        var model = CreateModel(db, "/chores");
        model.ActiveLabelId = 7;
        model.ActiveHouseholdIds = [2, 3];

        var path = model.BuildCompletePath(11);

        Assert.Equal("/chores/Chores/Complete?id=11&labelId=7&householdIds=2&householdIds=3", path);
    }

    [Fact]
    public void CompleteBuildDashboardPath_PreservesActiveFilters()
    {
        using var db = CreateDbContext();
        var model = new CompleteModel(db, new HouseholdMembershipService(db))
        {
            LabelId = 7,
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

        Assert.Equal("/chores/?labelId=7&householdIds=2&householdIds=3", path);
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
