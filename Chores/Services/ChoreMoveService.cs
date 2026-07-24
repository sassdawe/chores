using Chores.Data;
using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public class ChoreMoveService(AppDbContext db)
{
    public async Task<bool> TryMoveAsync(int choreId, int destinationHouseholdId, CancellationToken cancellationToken = default)
    {
        var chore = await db.Chores
            .Include(candidate => candidate.Labels)
            .FirstOrDefaultAsync(candidate => candidate.Id == choreId, cancellationToken);
        if (chore is null || chore.HouseholdId == destinationHouseholdId)
        {
            return false;
        }

        var destinationExists = await db.Households
            .AsNoTracking()
            .AnyAsync(household => household.Id == destinationHouseholdId, cancellationToken);
        if (!destinationExists)
        {
            return false;
        }

        var destinationUserIds = (await db.HouseholdMemberships
            .AsNoTracking()
            .Where(membership => membership.HouseholdId == destinationHouseholdId)
            .Select(membership => membership.UserId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var completionRecords = await db.CompletionRecords
            .Where(record => record.ChoreId == choreId)
            .ToListAsync(cancellationToken);

        AppUser? lostPlaceholder = null;
        foreach (var completionRecord in completionRecords)
        {
            if (destinationUserIds.Contains(completionRecord.CompletedByUserId))
            {
                continue;
            }

            lostPlaceholder ??= await GetLostPlaceholderAsync(cancellationToken);
            completionRecord.CompletedByUser = lostPlaceholder;
        }

        chore.HouseholdId = destinationHouseholdId;
        chore.Labels.Clear();

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<AppUser> GetLostPlaceholderAsync(CancellationToken cancellationToken)
    {
        var existingUser = await db.Users
            .FirstOrDefaultAsync(user => user.LoginName == LoginNameValidator.LostPlaceholderLoginName, cancellationToken);
        if (existingUser is not null)
        {
            return existingUser;
        }

        var lostPlaceholder = new AppUser
        {
            LoginName = LoginNameValidator.LostPlaceholderLoginName
        };

        db.Users.Add(lostPlaceholder);
        return lostPlaceholder;
    }
}
