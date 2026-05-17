using Chores.Data;
using Chores.Models;
using Chores.Pages.Members;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chores.Tests;

public class MembersIndexTests
{
    [Fact]
    public async Task OnPostCreateSpaceAsync_RejectsDuplicateSpaceNameForUser()
    {
        await using var db = CreateDbContext();
        var existingHousehold = new Household { Name = "Home" };
        var user = CreateUser("alice", existingHousehold, isOwner: true);

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = CreateModel(db, user.LoginName);
        model.SpaceName = " home ";

        var result = await model.OnPostCreateSpaceAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey(nameof(IndexModel.SpaceName)));
        Assert.Equal(1, await db.Households.CountAsync());
    }

    [Fact]
    public async Task OnPostCreateSpaceAsync_RejectsSpaceNameOverMaximumLength()
    {
        await using var db = CreateDbContext();
        var user = new AppUser { LoginName = "alice" };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = CreateModel(db, user.LoginName);
        model.SpaceName = new string('a', IndexModel.MaxSpaceNameLength + 1);

        var result = await model.OnPostCreateSpaceAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey(nameof(IndexModel.SpaceName)));
        Assert.Empty(db.Households);
    }

    [Fact]
    public async Task OnPostCreateSpaceAsync_NormalizesWhitespaceBeforeSaving()
    {
        await using var db = CreateDbContext();
        var user = new AppUser { LoginName = "alice" };

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = CreateModel(db, user.LoginName);
        model.SpaceName = "  Home   Office  ";

        await model.OnPostCreateSpaceAsync();

        Assert.Equal("Home Office", await db.Households.Select(household => household.Name).SingleAsync());
    }

    [Fact]
    public async Task OnPostRenameSpaceAsync_RejectsDuplicateSpaceNameForUser()
    {
        await using var db = CreateDbContext();
        var currentHousehold = new Household { Name = "Home" };
        var existingHousehold = new Household { Name = "Garage" };
        var user = CreateUser("alice", currentHousehold, isOwner: true);
        user.HouseholdMemberships.Add(new HouseholdMembership
        {
            Household = existingHousehold,
            IsOwner = true,
            JoinedAtUtc = DateTime.UtcNow
        });

        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = CreateModel(db, user.LoginName);
        model.RenameSpaceName = " garage ";

        var result = await model.OnPostRenameSpaceAsync(currentHousehold.Id);

        Assert.IsType<PageResult>(result);
        Assert.True(model.ModelState.ContainsKey(nameof(IndexModel.RenameSpaceName)));
        Assert.Equal("Home", await db.Households
            .Where(household => household.Id == currentHousehold.Id)
            .Select(household => household.Name)
            .SingleAsync());
    }

    private static AppUser CreateUser(string loginName, Household household, bool isOwner)
    {
        var user = new AppUser { LoginName = loginName };
        user.HouseholdMemberships.Add(new HouseholdMembership
        {
            Household = household,
            IsOwner = isOwner,
            JoinedAtUtc = DateTime.UtcNow
        });

        return user;
    }

    private static IndexModel CreateModel(AppDbContext db, string loginName)
    {
        return new IndexModel(db, new HouseholdInvitationService(db), new HouseholdMembershipService(db))
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
