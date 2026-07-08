using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.EntityFrameworkCore;

namespace Chores.Tests;

public class ChoreCompletionAdherenceQueryTests
{
    [Fact]
    public async Task GetLatestByChoreAsync_UsesLatestCompletionAndNeverDoneSentinel()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow.Date;
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alex" };
        var completedChore = new Chore { Name = "Dishes", Schedule = Schedule.Daily, Household = household };
        var neverDoneChore = new Chore { Name = "Windows", Schedule = Schedule.Weekly, Household = household };

        db.Users.Add(user);
        db.Chores.AddRange(completedChore, neverDoneChore);
        await db.SaveChangesAsync();

        var olderCompletion = now.AddDays(-4);
        var latestCompletion = now.AddDays(-2);
        db.CompletionRecords.AddRange(
            new CompletionRecord
            {
                ChoreId = completedChore.Id,
                CompletedByUserId = user.Id,
                CompletedAtUtc = olderCompletion
            },
            new CompletionRecord
            {
                ChoreId = completedChore.Id,
                CompletedByUserId = user.Id,
                CompletedAtUtc = latestCompletion
            });
        await db.SaveChangesAsync();

        var adherence = new ScheduleAdherenceService();
        var result = await ChoreCompletionAdherenceQuery.GetLatestByChoreAsync(
            db,
            [completedChore, neverDoneChore],
            adherence);

        Assert.Equal(latestCompletion, result[completedChore.Id].LastCompletedAtUtc);
        Assert.Equal(AdherenceStatus.Overdue, result[neverDoneChore.Id].Adherence.Status);
        Assert.Equal(int.MaxValue, result[neverDoneChore.Id].Adherence.DaysOverdue);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
