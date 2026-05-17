using Chores.Data;
using Chores.Models;
using Chores.Pages.Profile;
using Chores.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;

namespace Chores.Tests;

public class ProfileExportTests
{
    [Fact]
    public async Task OnGetExportAsync_ReturnsHouseholdDataAsJsonFile()
    {
        await using var db = CreateDbContext();

        var household = new Household { Name = "Home" };
        var owner = new AppUser { LoginName = "alice" };
        var member = new AppUser { LoginName = "bob" };
        var label = new Label
        {
            Name = "Kitchen",
            Color = "#123456",
            Household = household
        };
        var chore = new Chore
        {
            Name = "Dishes",
            Schedule = Schedule.Weekly,
            Household = household
        };
        chore.Labels.Add(label);

        var completion = new CompletionRecord
        {
            Chore = chore,
            CompletedByUser = member,
            CompletedAtUtc = new DateTime(2026, 5, 14, 9, 30, 0, DateTimeKind.Utc)
        };

        db.Users.AddRange(owner, member);
        db.HouseholdMemberships.AddRange(
            new HouseholdMembership { User = owner, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow },
            new HouseholdMembership { User = member, Household = household, IsOwner = false, JoinedAtUtc = DateTime.UtcNow });
        db.Labels.Add(label);
        db.Chores.Add(chore);
        db.CompletionRecords.Add(completion);
        await db.SaveChangesAsync();

        var model = new IndexModel(db, new HouseholdInvitationService(db), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, owner.LoginName)
                    ],
                    "TestAuth"))
                }
            }
        };

        var result = await model.OnGetExportAsync();

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json; charset=utf-8", fileResult.ContentType);
        Assert.StartsWith("chores-export-", fileResult.FileDownloadName);

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(fileResult.FileContents));
        var root = document.RootElement;
        var exportedAtUtc = root.GetProperty("exportedAtUtc").GetDateTime();

        Assert.Equal("alice", root.GetProperty("exportedByLoginName").GetString());
        Assert.Equal("Home", root.GetProperty("household").GetProperty("name").GetString());
        Assert.Equal($"chores-export-{exportedAtUtc:yyyyMMddHHmmss}.json", fileResult.FileDownloadName);

        var labels = root.GetProperty("labels");
        Assert.Single(labels.EnumerateArray());
        Assert.Equal("Kitchen", labels[0].GetProperty("name").GetString());
        Assert.Equal("#123456", labels[0].GetProperty("color").GetString());

        var chores = root.GetProperty("chores");
        Assert.Single(chores.EnumerateArray());
        Assert.Equal("Dishes", chores[0].GetProperty("name").GetString());
        Assert.Equal("Weekly", chores[0].GetProperty("schedule").GetString());

        var choreLabels = chores[0].GetProperty("labels");
        Assert.Single(choreLabels.EnumerateArray());
        Assert.Equal("Kitchen", choreLabels[0].GetProperty("name").GetString());

        var history = chores[0].GetProperty("completionHistory");
        Assert.Single(history.EnumerateArray());
        Assert.Equal("bob", history[0].GetProperty("completedByLoginName").GetString());
        Assert.Equal("2026-05-14T09:30:00Z", history[0].GetProperty("completedAtUtc").GetString());
    }

    [Fact]
    public async Task OnGetChoreListExportAsync_ReturnsOnlyChoreNamesAndSchedules()
    {
        await using var db = CreateDbContext();

        var household = new Household { Name = "Home" };
        var owner = new AppUser { LoginName = "alice" };
        var member = new AppUser { LoginName = "bob" };
        var label = new Label
        {
            Name = "Kitchen",
            Color = "#123456",
            Household = household
        };
        var chore = new Chore
        {
            Name = "Dishes",
            Schedule = Schedule.Weekly,
            Household = household
        };
        chore.Labels.Add(label);

        db.Users.AddRange(owner, member);
        db.HouseholdMemberships.AddRange(
            new HouseholdMembership { User = owner, Household = household, IsOwner = true, JoinedAtUtc = DateTime.UtcNow },
            new HouseholdMembership { User = member, Household = household, IsOwner = false, JoinedAtUtc = DateTime.UtcNow });
        db.Labels.Add(label);
        db.Chores.Add(chore);
        db.CompletionRecords.Add(new CompletionRecord
        {
            Chore = chore,
            CompletedByUser = member,
            CompletedAtUtc = new DateTime(2026, 5, 14, 9, 30, 0, DateTimeKind.Utc)
        });
        await db.SaveChangesAsync();

        var model = new IndexModel(db, new HouseholdInvitationService(db), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, owner.LoginName)
                    ],
                    "TestAuth"))
                }
            }
        };

        var result = await model.OnGetChoreListExportAsync();

        var fileResult = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/json; charset=utf-8", fileResult.ContentType);
        Assert.StartsWith("chores-list-export-", fileResult.FileDownloadName);

        using var document = JsonDocument.Parse(Encoding.UTF8.GetString(fileResult.FileContents));
        var root = document.RootElement;
        var exportedAtUtc = root.GetProperty("exportedAtUtc").GetDateTime();

        Assert.Equal("alice", root.GetProperty("exportedByLoginName").GetString());
        Assert.False(root.TryGetProperty("household", out _));
        Assert.False(root.TryGetProperty("labels", out _));
        Assert.Equal($"chores-list-export-{exportedAtUtc:yyyyMMddHHmmss}.json", fileResult.FileDownloadName);

        var chores = root.GetProperty("chores");
        Assert.Single(chores.EnumerateArray());
        Assert.Equal("Dishes", chores[0].GetProperty("name").GetString());
        Assert.Equal("Weekly", chores[0].GetProperty("schedule").GetString());
        Assert.False(chores[0].TryGetProperty("labels", out _));
        Assert.False(chores[0].TryGetProperty("completionHistory", out _));
        Assert.False(chores[0].TryGetProperty("id", out _));
    }

    [Fact]
    public async Task OnPostExportHandlers_ReturnJsonFilesForProfileForms()
    {
        await using var db = CreateDbContext();

        var household = new Household { Name = "Home" };
        var owner = new AppUser { LoginName = "alice" };
        var chore = new Chore
        {
            Name = "Laundry",
            Schedule = Schedule.Monthly,
            Household = household
        };

        db.Users.Add(owner);
        db.HouseholdMemberships.Add(new HouseholdMembership
        {
            User = owner,
            Household = household,
            IsOwner = true,
            JoinedAtUtc = DateTime.UtcNow
        });
        db.Chores.Add(chore);
        await db.SaveChangesAsync();

        var model = new IndexModel(db, new HouseholdInvitationService(db), new HouseholdMembershipService(db))
        {
            PageContext = new PageContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                    [
                        new Claim(ClaimTypes.Name, owner.LoginName)
                    ],
                    "TestAuth"))
                }
            }
        };

        var fullExport = Assert.IsType<FileContentResult>(await model.OnPostExportAsync());
        var minimalExport = Assert.IsType<FileContentResult>(await model.OnPostChoreListExportAsync());

        Assert.StartsWith("chores-export-", fullExport.FileDownloadName);
        Assert.StartsWith("chores-list-export-", minimalExport.FileDownloadName);
    }

    private static AppDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
