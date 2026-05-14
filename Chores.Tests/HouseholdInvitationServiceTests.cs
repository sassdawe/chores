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
        invitedUser.Credentials.Add(new FidoCredential
        {
            CredentialId = [1],
            PublicKey = [2],
            UserHandle = [3],
            CredType = "public-key",
            RegDate = DateTime.UtcNow.AddHours(-2)
        });

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

    [Fact]
    public async Task GetPendingInvitesAsync_HidesInvitesCreatedBeforeRegistration()
    {
        await using var db = CreateDbContext();
        var sourceHousehold = new Household { Name = "Source" };
        var destinationHousehold = new Household { Name = "Destination" };
        var owner = new AppUser
        {
            LoginName = "owner",
            Household = destinationHousehold,
            IsHouseholdOwner = true
        };
        var invitedUser = new AppUser
        {
            LoginName = "invitee",
            Household = sourceHousehold,
            IsHouseholdOwner = false
        };
        invitedUser.Credentials.Add(new FidoCredential
        {
            CredentialId = [1],
            PublicKey = [2],
            UserHandle = [3],
            CredType = "public-key",
            RegDate = DateTime.UtcNow.AddHours(-1)
        });

        db.Users.AddRange(owner, invitedUser);
        await db.SaveChangesAsync();

        var oldInvite = new HouseholdInvite
        {
            HouseholdId = destinationHousehold.Id,
            InvitedByUserId = owner.Id,
            LoginName = invitedUser.LoginName,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };
        var newInvite = new HouseholdInvite
        {
            HouseholdId = destinationHousehold.Id,
            InvitedByUserId = owner.Id,
            LoginName = invitedUser.LoginName,
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-15)
        };

        db.HouseholdInvites.AddRange(oldInvite, newInvite);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        var invites = await sut.GetPendingInvitesAsync(invitedUser);

        Assert.Single(invites);
        Assert.Equal(newInvite.Id, invites[0].Id);
        Assert.DoesNotContain(db.HouseholdInvites, invite => invite.Id == oldInvite.Id);
    }

    [Fact]
    public async Task CreateInviteAsync_DiscardsExpiredInviteBeforeCreatingReplacement()
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

        db.HouseholdInvites.Add(new HouseholdInvite
        {
            HouseholdId = household.Id,
            InvitedByUserId = owner.Id,
            LoginName = "invitee",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-6)
        });
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        await sut.CreateInviteAsync(owner, "invitee");

        var invites = db.HouseholdInvites.OrderBy(invite => invite.CreatedAtUtc).ToList();
        Assert.Single(invites);
        Assert.True(invites[0].CreatedAtUtc > DateTime.UtcNow.AddDays(-5));
    }

    [Fact]
    public async Task AcceptInviteAsync_RejectsInviteCreatedBeforeRegistration()
    {
        await using var db = CreateDbContext();
        var currentHousehold = new Household { Name = "Current" };
        var invitedHousehold = new Household { Name = "Invited" };
        var owner = new AppUser
        {
            LoginName = "owner",
            Household = invitedHousehold,
            IsHouseholdOwner = true
        };
        var invitedUser = new AppUser
        {
            LoginName = "invitee",
            Household = currentHousehold,
            IsHouseholdOwner = false
        };
        invitedUser.Credentials.Add(new FidoCredential
        {
            CredentialId = [1],
            PublicKey = [2],
            UserHandle = [3],
            CredType = "public-key",
            RegDate = DateTime.UtcNow.AddHours(-1)
        });

        db.Users.AddRange(owner, invitedUser);
        await db.SaveChangesAsync();

        var invite = new HouseholdInvite
        {
            HouseholdId = invitedHousehold.Id,
            InvitedByUserId = owner.Id,
            LoginName = invitedUser.LoginName,
            CreatedAtUtc = DateTime.UtcNow.AddHours(-2)
        };

        db.HouseholdInvites.Add(invite);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        var accepted = await sut.AcceptInviteAsync(invitedUser, invite.Id);

        Assert.False(accepted);
        Assert.Equal(currentHousehold.Id, invitedUser.HouseholdId);
        Assert.DoesNotContain(db.HouseholdInvites, pendingInvite => pendingInvite.Id == invite.Id);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
