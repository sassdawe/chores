using Chores.Data;
using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public class HouseholdMembershipService(AppDbContext db)
{
    public async Task<AppUser?> GetUserAsync(string? loginName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return null;
        }

        return await db.Users.FirstOrDefaultAsync(user => user.LoginName == loginName, cancellationToken);
    }

    public async Task<List<HouseholdMembership>> GetMembershipsAsync(string? loginName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return [];
        }

        return await db.HouseholdMemberships
            .AsNoTracking()
            .Include(membership => membership.Household)
            .Where(membership => membership.User.LoginName == loginName)
            .OrderBy(membership => membership.Household.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<int>> GetHouseholdIdsAsync(string? loginName, CancellationToken cancellationToken = default)
    {
        return (await GetMembershipsAsync(loginName, cancellationToken))
            .Select(membership => membership.HouseholdId)
            .ToList();
    }

    public async Task<HouseholdMembership?> GetMembershipAsync(string? loginName, int householdId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(loginName))
        {
            return null;
        }

        return await db.HouseholdMemberships
            .AsNoTracking()
            .Include(membership => membership.Household)
            .FirstOrDefaultAsync(membership => membership.User.LoginName == loginName
                && membership.HouseholdId == householdId, cancellationToken);
    }

    public async Task<bool> CanAccessHouseholdAsync(string? loginName, int householdId, CancellationToken cancellationToken = default)
    {
        return await GetMembershipAsync(loginName, householdId, cancellationToken) is not null;
    }

    public async Task<bool> IsOwnerAsync(string? loginName, int householdId, CancellationToken cancellationToken = default)
    {
        var membership = await GetMembershipAsync(loginName, householdId, cancellationToken);
        return membership?.IsOwner == true;
    }

    public async Task<int?> GetDefaultHouseholdIdAsync(string? loginName, CancellationToken cancellationToken = default)
    {
        return (await GetMembershipsAsync(loginName, cancellationToken)).FirstOrDefault()?.HouseholdId;
    }
}
