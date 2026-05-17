using Chores.Data;
using Chores.Models;
using Chores.Pages.Chores;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Chores.Tests;

public class ChoreCreateTests
{
    [Fact]
    public async Task OnPostAsync_ReturnsPageErrorWhenUserHasNoSpaces()
    {
        await using var db = CreateDbContext();
        var user = new AppUser { LoginName = "alice" };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var model = new CreateModel(db, new HouseholdMembershipService(db))
        {
            HouseholdId = 0,
            Name = "Dishes",
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, user.LoginName)
                    ],
                    "TestAuth"))
                }
            }
        };

        var result = await model.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Empty(model.Spaces);
        Assert.True(model.ModelState.ContainsKey(nameof(CreateModel.HouseholdId)));
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
