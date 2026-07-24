using Chores.Data;
using Chores.Models;
using Chores.Pages.Chores;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.RegularExpressions;

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
        Assert.InRange(model.LastCompletionAdherence.DaysOverdue, 3, 4);
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

    [Fact]
    public void CompletePage_RendersYesterdayActionAsPostForm()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Chores", "Pages", "Chores", "Complete.cshtml"));

        var markup = File.ReadAllText(pagePath);

        Assert.Contains("<form method=\"post\" asp-page-handler=\"Yesterday\" asp-route-id=\"@Model.Chore.Id\">", markup);
        Assert.Contains("complete.doneYesterday", markup);
    }

    [Fact]
    public async Task OnPostSkipAsync_SavesSkippedRecord()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var chore = new Chore { Name = "Vacuum", Household = household, Schedule = Schedule.Weekly };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, user.LoginName);
        var beforeSaveUtc = DateTime.UtcNow;

        var result = await model.OnPostSkipAsync(chore.Id);

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);

        var savedRecord = await db.CompletionRecords.SingleAsync();
        Assert.Equal(chore.Id, savedRecord.ChoreId);
        Assert.True(savedRecord.IsSkipped);
        Assert.InRange(savedRecord.CompletedAtUtc, beforeSaveUtc.AddSeconds(-5), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task OnPostAsync_SavesNonSkippedRecord()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var chore = new Chore { Name = "Mop", Household = household, Schedule = Schedule.Weekly };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership { User = user, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var model = CreateAuthenticatedModel(db, user.LoginName);
        model.CompletedAt = DateTime.Now;

        await model.OnPostAsync(chore.Id);

        var savedRecord = await db.CompletionRecords.SingleAsync();
        Assert.False(savedRecord.IsSkipped);
    }

    [Fact]
    public void CompletePage_RendersSkipActionAsPostForm()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Chores", "Pages", "Chores", "Complete.cshtml"));

        var markup = File.ReadAllText(pagePath);

        Assert.Contains("asp-page-handler=\"Skip\"", markup);
        Assert.Contains("complete.skip", markup);
    }

    [Fact]
    public void HistoryPage_RendersSkippedColumn()
    {
        var pagePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..",
            "Chores", "Pages", "Chores", "History.cshtml"));

        var markup = File.ReadAllText(pagePath);

        Assert.Contains("Skipped", markup);
        Assert.Contains("IsSkipped", markup);
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
