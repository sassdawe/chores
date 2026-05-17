using Chores.Data;
using Chores.Models;
using Chores.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chores.Tests;

public class HouseholdInvitationServiceTests
{
    [Fact]
    public async Task CreateInviteAsync_RequiresSpaceOwner()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var inviter = CreateUser("member", household, isOwner: false);

        db.Users.Add(inviter);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => sut.CreateInviteAsync(inviter, household.Id, "invitee"));
    }

    [Fact]
    public async Task CreateInviteAsync_CreatesSinglePendingInvite()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var owner = CreateUser("owner", household, isOwner: true);

        db.Users.Add(owner);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);

        await sut.CreateInviteAsync(owner, household.Id, "invitee");
        await sut.CreateInviteAsync(owner, household.Id, "invitee");

        Assert.Single(db.HouseholdInvites);
    }

    [Fact]
    public async Task CreateInviteAsync_IgnoresExistingSpaceMember()
    {
        await using var db = CreateDbContext();
        var household = new Household { Name = "Home" };
        var owner = CreateUser("owner", household, isOwner: true);
        var member = CreateUser("member", household, isOwner: false);

        db.Users.AddRange(owner, member);
        await db.SaveChangesAsync();

        var sut = new HouseholdInvitationService(db);
        await sut.CreateInviteAsync(owner, household.Id, member.LoginName);

        Assert.Empty(db.HouseholdInvites);
    }

    [Fact]
    public async Task AcceptInviteAsync_AddsMembershipAndKeepsOtherInvitesPending()
    {
        await using var db = CreateDbContext();
        var sourceHousehold = new Household { Name = "Source" };
        var destinationHousehold = new Household { Name = "Destination" };
        var anotherHousehold = new Household { Name = "Another" };

        var owner = CreateUser("owner", destinationHousehold, isOwner: true);
        var anotherOwner = CreateUser("another-owner", anotherHousehold, isOwner: true);
        var invitedUser = CreateUser("invitee", sourceHousehold, isOwner: true);

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

        var memberships = await db.HouseholdMemberships
            .Where(membership => membership.UserId == invitedUser.Id)
            .Select(membership => membership.HouseholdId)
            .ToListAsync();

        Assert.True(accepted);
        Assert.Contains(sourceHousehold.Id, memberships);
        Assert.Contains(destinationHousehold.Id, memberships);
        Assert.NotNull(primaryInvite.AcceptedAtUtc);
        Assert.Null(competingInvite.DeclinedAtUtc);
    }

    [Fact]
    public async Task AcceptInviteAsync_AllowsEstablishedOwnerToJoinAnotherSpace()
    {
        await using var db = CreateDbContext();
        var currentHousehold = new Household { Name = "Current" };
        var invitedHousehold = new Household { Name = "Invited" };
        var owner = CreateUser("owner", currentHousehold, isOwner: true);
        var invitingOwner = CreateUser("inviting-owner", invitedHousehold, isOwner: true);
        var currentChore = new Chore
        {
            Name = "Keep current household",
            Household = currentHousehold,
            Schedule = Schedule.Weekly
        };

        db.Users.AddRange(owner, invitingOwner);
        db.Chores.Add(currentChore);
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

        Assert.True(accepted);
        Assert.NotNull(invite.AcceptedAtUtc);
        Assert.True(await db.HouseholdMemberships.AnyAsync(membership => membership.UserId == owner.Id
            && membership.HouseholdId == currentHousehold.Id
            && membership.IsOwner));
        Assert.True(await db.HouseholdMemberships.AnyAsync(membership => membership.UserId == owner.Id
            && membership.HouseholdId == invitedHousehold.Id
            && !membership.IsOwner));
    }

    [Fact]
    public async Task GetPendingInvitesAsync_HidesInvitesCreatedBeforeRegistration()
    {
        await using var db = CreateDbContext();
        var sourceHousehold = new Household { Name = "Source" };
        var destinationHousehold = new Household { Name = "Destination" };
        var owner = CreateUser("owner", destinationHousehold, isOwner: true);
        var invitedUser = CreateUser("invitee", sourceHousehold, isOwner: false, credentialAge: TimeSpan.FromHours(1));

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
        var owner = CreateUser("owner", household, isOwner: true);

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
        await sut.CreateInviteAsync(owner, household.Id, "invitee");

        var invites = db.HouseholdInvites.OrderBy(invite => invite.CreatedAtUtc).ToList();
        Assert.Single(invites);
        Assert.True(invites[0].CreatedAtUtc > DateTime.UtcNow.AddDays(-5));
    }

    [Fact]
    public async Task PendingInviteIndex_RejectsDuplicatePendingInviteForSameHouseholdAndLogin()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var db = CreateSqliteDbContext(connection);
        await db.Database.EnsureCreatedAsync();

        var household = new Household { Name = "Home" };
        var owner = CreateUser("owner", household, isOwner: true);

        db.Users.Add(owner);
        await db.SaveChangesAsync();

        db.HouseholdInvites.Add(new HouseholdInvite
        {
            HouseholdId = household.Id,
            InvitedByUserId = owner.Id,
            LoginName = "invitee",
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2)
        });
        await db.SaveChangesAsync();

        db.HouseholdInvites.Add(new HouseholdInvite
        {
            HouseholdId = household.Id,
            InvitedByUserId = owner.Id,
            LoginName = "invitee",
            CreatedAtUtc = DateTime.UtcNow
        });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AcceptInviteAsync_RejectsInviteCreatedBeforeRegistration()
    {
        await using var db = CreateDbContext();
        var currentHousehold = new Household { Name = "Current" };
        var invitedHousehold = new Household { Name = "Invited" };
        var owner = CreateUser("owner", invitedHousehold, isOwner: true);
        var invitedUser = CreateUser("invitee", currentHousehold, isOwner: false, credentialAge: TimeSpan.FromHours(1));

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
        Assert.False(await db.HouseholdMemberships.AnyAsync(membership => membership.UserId == invitedUser.Id
            && membership.HouseholdId == invitedHousehold.Id));
        Assert.DoesNotContain(db.HouseholdInvites, pendingInvite => pendingInvite.Id == invite.Id);
    }

    private static AppUser CreateUser(
        string loginName,
        Household household,
        bool isOwner,
        TimeSpan? credentialAge = null)
    {
        var user = new AppUser { LoginName = loginName };
        user.HouseholdMemberships.Add(new HouseholdMembership
        {
            Household = household,
            IsOwner = isOwner,
            JoinedAtUtc = DateTime.UtcNow
        });
        user.Credentials.Add(new FidoCredential
        {
            CredentialId = Guid.NewGuid().ToByteArray(),
            PublicKey = Guid.NewGuid().ToByteArray(),
            UserHandle = Guid.NewGuid().ToByteArray(),
            CredType = "public-key",
            RegDate = DateTime.UtcNow - (credentialAge ?? TimeSpan.FromHours(2))
        });

        return user;
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }

    private static AppDbContext CreateSqliteDbContext(SqliteConnection connection)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }
}
