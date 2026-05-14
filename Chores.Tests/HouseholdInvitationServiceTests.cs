using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.EntityFrameworkCore;

namespace Chores.Tests;

public class HouseholdInvitationServiceTests
{
    [Fact]
    public async Task CreateInviteAsync_RequiresHouseholdOwner()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var inviter = new AppUser
        {
            LoginName = "member",
            Household = household,
            IsHouseholdOwner = false
        };

        db.Users.Add(inviter);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateInviteAsync(inviter, "invitee"));
    }

    [Fact]
    public async Task CreateInviteAsync_CreatesSinglePendingInvite()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var owner = new AppUser
        {
            LoginName = "owner",
            Household = household,
            IsHouseholdOwner = true
        };

        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);

        await sut.CreateInviteAsync(owner, "invitee");
        await sut.CreateInviteAsync(owner, "invitee");

        Assert.Single(db.HouseholdInvites);
    }

    [Fact]
    public async Task AcceptInviteAsync_MovesUserAndClosesCompetingInvites()
    {
        await using var db = CreateDbContext();
        var sourceHousehold = new Household { Name = "Source" };
        var destinationHousehold = new Household { Name = "Destination" };
        var anotherHousehold = new Household { Name = "Another" };

        var owner = new AppUser
        {
            LoginName = "owner",
            Household = destinationHousehold,
            IsHouseholdOwner = true
        };

        var anotherOwner = new AppUser
        {
            LoginName = "another-owner",
            Household = anotherHousehold,
            IsHouseholdOwner = true
        };

        var invitedUser = new AppUser
        {
            LoginName = "invitee",
            Household = sourceHousehold,
            IsHouseholdOwner = false
        };

        db.Users.AddRange(owner, anotherOwner, invitedUser);
        await db.SaveChangesAsync();

        var primaryInvite = new HouseholdInvite
        {
            HouseholdId = destinationHousehold.Id,
            InvitedByUserId = owner.Id,
            LoginName = invitedUser.LoginName,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1)
        };
        var competingInvite = new HouseholdInvite
        {
            HouseholdId = anotherHousehold.Id,
            InvitedByUserId = anotherOwner.Id,
            LoginName = invitedUser.LoginName,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.HouseholdInvites.AddRange(primaryInvite, competingInvite);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        var accepted = await sut.AcceptInviteAsync(invitedUser, primaryInvite.Id);

        Assert.True(accepted);
        Assert.Equal(destinationHousehold.Id, invitedUser.HouseholdId);
        Assert.NotNull(primaryInvite.AcceptedAtUtc);
        Assert.NotNull(competingInvite.DeclinedAtUtc);
    }

    [Fact]
    public async Task AcceptInviteAsync_RejectsHouseholdOwner()
    {
        await using var db = CreateDbContext();
        var currentHousehold = new Household { Name = "Current" };
        var invitedHousehold = new Household { Name = "Invited" };
        var owner = new AppUser
        {
            LoginName = "owner",
            Household = currentHousehold,
            IsHouseholdOwner = true
        };
        var invitingOwner = new AppUser
        {
            LoginName = "inviting-owner",
            Household = invitedHousehold,
            IsHouseholdOwner = true
        };

        db.Users.AddRange(owner, invitingOwner);
        await db.SaveChangesAsync();

        var invite = new HouseholdInvite
        {
            HouseholdId = invitedHousehold.Id,
            InvitedByUserId = invitingOwner.Id,
            LoginName = owner.LoginName,
            CreatedAtUtc = DateTime.UtcNow
        };

        db.HouseholdInvites.Add(invite);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        var accepted = await sut.AcceptInviteAsync(owner, invite.Id);

        Assert.False(accepted);
        Assert.Null(invite.AcceptedAtUtc);
        Assert.Equal(currentHousehold.Id, owner.HouseholdId);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
