using Chores.Data;
using Chores.Models;
using Chores.Pages.Chores;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chores.Tests;

public class CompleteModelTests
{
    [Fact]
    public async Task OnGetAsync_LoadsLatestCompletionStatus()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var chore = new Chore { Name = "Dishes", Household = household, Schedule = Schedule.Weekly };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var lastCompletedUtc = DateTime.UtcNow.AddDays(-10);
        db.CompletionRecords.Add(new CompletionRecord
        {
            ChoreId = chore.Id,
            CompletedByUserId = user.Id,
            CompletedAtUtc = lastCompletedUtc
        });
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, user.LoginName);

        var result = await model.OnGetAsync(chore.Id);

        Assert.IsType<PageResult>(result);
        Assert.Equal(lastCompletedUtc, model.LastCompletedUtc);
        Assert.NotNull(model.LastCompletionAdherence);
        Assert.Equal(AdherenceStatus.Overdue, model.LastCompletionAdherence!.Status);
        Assert.Equal(3, model.LastCompletionAdherence.DaysOverdue);
    }

    [Fact]
    public async Task OnPostYesterdayAsync_SavesCompletionTwentyFourHoursAgo()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var chore = new Chore { Name = "Laundry", Household = household, Schedule = Schedule.Weekly };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, user.LoginName);
        var beforeSaveUtc = DateTime.UtcNow;

        var result = await model.OnPostYesterdayAsync(chore.Id);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        var savedCompletion = await db.CompletionRecords.SingleAsync();
        Assert.Equal(chore.Id, savedCompletion.ChoreId);
        Assert.InRange(savedCompletion.CompletedAtUtc, beforeSaveUtc.AddHours(-24).AddSeconds(-5), DateTime.UtcNow.AddHours(-24).AddSeconds(5));
    }

    private static CompleteModel CreateAuthenticatedModel(AppDbContext db, string loginName)
    {
        return new CompleteModel(db, new ScheduleAdherenceService(), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
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
