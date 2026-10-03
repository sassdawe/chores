using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.EntityFrameworkCore;

namespace Chores.Tests;

public class ChoreMoveServiceTests
{
    [Fact]
    public async Task TryMoveAsync_ReplacesMissingCompletionUsersAndClearsLabels()
    {
        await using var db = CreateDbContext();
        var sourceHousehold = new Household { Name = "Home" };
        var destinationHousehold = new Household { Name = "Cabin" };
        var retainedUser = new AppUser { LoginName = "alice" };
        var missingUser = new AppUser { LoginName = "bob" };
        var sourceLabel = new Label { Name = "Kitchen", Color = "#123456", Household = sourceHousehold };
        var chore = new Chore { Name = "Dishes", Schedule = Schedule.Daily, Household = sourceHousehold };

        chore.Labels.Add(sourceLabel);
        db.Users.AddRange(retainedUser, missingUser);
        db.HouseholdMemberships.AddRange(
            new HouseholdMembership { User = retainedUser, Household = sourceHousehold, IsOwner = true, JoinedAtUtc = DateTime.UtcNow },
            new HouseholdMembership { User = retainedUser, Household = destinationHousehold, IsOwner = true, JoinedAtUtc = DateTime.UtcNow },
            new HouseholdMembership { User = missingUser, Household = sourceHousehold, IsOwner = false, JoinedAtUtc = DateTime.UtcNow });
        db.Chores.Add(chore);
        db.CompletionRecords.AddRange(
            new CompletionRecord { Chore = chore, CompletedByUser = retainedUser, CompletedAtUtc = DateTime.UtcNow.AddDays(-2) },
            new CompletionRecord { Chore = chore, CompletedByUser = missingUser, CompletedAtUtc = DateTime.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var service = new ChoreMoveService(db);

        var moved = await service.TryMoveAsync(chore.Id, destinationHousehold.Id);

        Assert.True(moved);

        var updatedChore = await db.Chores
            .Include(updated => updated.Labels)
            .SingleAsync(updated => updated.Id == chore.Id);
        Assert.Equal(destinationHousehold.Id, updatedChore.HouseholdId);
        Assert.Empty(updatedChore.Labels);

        var records = await db.CompletionRecords
            .Include(record => record.CompletedByUser)
            .Where(record => record.ChoreId == chore.Id)
            .OrderBy(record => record.CompletedAtUtc)
            .ToListAsync();
        Assert.Equal("alice", records[0].CompletedByUser.LoginName);
        Assert.Equal(LoginNameValidator.LostPlaceholderLoginName, records[1].CompletedByUser.LoginName);

        var lostUser = await db.Users.SingleAsync(user => user.LoginName == LoginNameValidator.LostPlaceholderLoginName);
        Assert.False(await db.HouseholdMemberships.AnyAsync(membership => membership.UserId == lostUser.Id));
    }

    [Fact]
    public async Task TryMoveAsync_ReturnsFalseWhenDestinationMatchesCurrentHousehold()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var chore = new Chore { Name = "Dishes", Schedule = Schedule.Daily, Household = household };

        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var service = new ChoreMoveService(db);

        var moved = await service.TryMoveAsync(chore.Id, household.Id);

        Assert.False(moved);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
