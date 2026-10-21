using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.EntityFrameworkCore;

namespace Chores.Tests;

public class ChoreCompletionServiceTests
{
    [Fact]
    public async Task RecordAsync_AppendsCompletionWithClientRecordId()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);
        var completedAtUtc = DateTime.UtcNow.AddHours(-3);

        var outcome = await service.RecordAsync(user.LoginName, chore.Id, completedAtUtc, clientRecordId: "abc123");

        Assert.Equal(ChoreCompletionOutcome.Recorded, outcome);
        var saved = await db.CompletionRecords.SingleAsync();
        Assert.Equal(chore.Id, saved.ChoreId);
        Assert.Equal(user.Id, saved.CompletedByUserId);
        Assert.Equal("abc123", saved.ClientRecordId);
        Assert.False(saved.IsSkipped);
    }

    [Fact]
    public async Task RecordAsync_ReplayingSameClientRecordId_DoesNotDuplicate()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);
        var completedAtUtc = DateTime.UtcNow.AddHours(-1);

        var first = await service.RecordAsync(user.LoginName, chore.Id, completedAtUtc, clientRecordId: "queued-1");
        var second = await service.RecordAsync(user.LoginName, chore.Id, completedAtUtc, clientRecordId: "queued-1");

        Assert.Equal(ChoreCompletionOutcome.Recorded, first);
        Assert.Equal(ChoreCompletionOutcome.Duplicate, second);
        Assert.Equal(1, await db.CompletionRecords.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_WithoutClientRecordId_AllowsRepeatedCompletions()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);

        await service.RecordAsync(user.LoginName, chore.Id, DateTime.UtcNow.AddHours(-2));
        await service.RecordAsync(user.LoginName, chore.Id, DateTime.UtcNow.AddHours(-1));

        Assert.Equal(2, await db.CompletionRecords.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_ClampsTimestampsFromSkewedDeviceClocks()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);

        await service.RecordAsync(user.LoginName, chore.Id, DateTime.UtcNow.AddDays(3), clientRecordId: "skewed");

        var saved = await db.CompletionRecords.SingleAsync();
        Assert.InRange(saved.CompletedAtUtc, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task RecordAsync_TreatsUnspecifiedKindAsUtc()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);
        var unspecified = DateTime.SpecifyKind(DateTime.UtcNow.AddHours(-5), DateTimeKind.Unspecified);

        await service.RecordAsync(user.LoginName, chore.Id, unspecified, clientRecordId: "unspecified");

        var saved = await db.CompletionRecords.SingleAsync();
        Assert.Equal(unspecified, saved.CompletedAtUtc);
    }

    [Fact]
    public async Task RecordAsync_PreservesSkippedFlag()
    {
        await using var db = CreateDbContext();
        var (user, chore) = await SeedAsync(db);
        var service = CreateService(db);

        await service.RecordAsync(user.LoginName, chore.Id, DateTime.UtcNow, isSkipped: true, clientRecordId: "skip-1");

        var saved = await db.CompletionRecords.SingleAsync();
        Assert.True(saved.IsSkipped);
    }

    [Fact]
    public async Task RecordAsync_UserOutsideSpace_IsRejected()
    {
        await using var db = CreateDbContext();
        var (_, chore) = await SeedAsync(db);
        var outsider = new AppUser { LoginName = "mallory" };
        db.Users.Add(outsider);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var outcome = await service.RecordAsync(outsider.LoginName, chore.Id, DateTime.UtcNow, clientRecordId: "intruder");

        Assert.Equal(ChoreCompletionOutcome.NotFound, outcome);
        Assert.Equal(0, await db.CompletionRecords.CountAsync());
    }

    [Fact]
    public async Task RecordAsync_UnknownChore_IsRejected()
    {
        await using var db = CreateDbContext();
        var (user, _) = await SeedAsync(db);
        var service = CreateService(db);

        var outcome = await service.RecordAsync(user.LoginName, 99999, DateTime.UtcNow, clientRecordId: "ghost");

        Assert.Equal(ChoreCompletionOutcome.NotFound, outcome);
    }

    [Fact]
    public async Task RecordAsync_UnknownUser_IsRejected()
    {
        await using var db = CreateDbContext();
        var (_, chore) = await SeedAsync(db);
        var service = CreateService(db);

        var outcome = await service.RecordAsync("nobody", chore.Id, DateTime.UtcNow, clientRecordId: "ghost-user");

        Assert.Equal(ChoreCompletionOutcome.NotFound, outcome);
    }

    private static async Task<(AppUser User, Chore Chore)> SeedAsync(AppDbContext db)
    {
        var household = new Household { Name = "Home" };
        var user = new AppUser { LoginName = "alice" };
        var chore = new Chore { Name = "Dishes", Household = household, Schedule = Schedule.Weekly };

        db.Households.Add(household);
        db.Users.Add(user);
        db.HouseholdMemberships.Add(new HouseholdMembership
        {
            User = user,
            Household = household,
            IsOwner = true,
            JoinedAtUtc = DateTime.UtcNow
        });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        return (user, chore);
    }

    private static ChoreCompletionService CreateService(AppDbContext db)
        => new(db, new HouseholdMembershipService(db));

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
