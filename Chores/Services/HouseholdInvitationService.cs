using Chores.Data;
using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public class HouseholdInvitationService(AppDbContext db)
{
    public async Task<List<HouseholdInvite>> GetPendingInvitesAsync(string loginName, CancellationToken cancellationToken = default)
    {
        return await db.HouseholdInvites
            .AsNoTracking()
            .Include(invite => invite.Household)
            .Include(invite => invite.InvitedByUser)
            .Where(invite => invite.LoginName == loginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<HouseholdInvite?> GetLatestPendingInviteAsync(string loginName, CancellationToken cancellationToken = default)
    {
        return await db.HouseholdInvites
            .Where(invite => invite.LoginName == loginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task CreateInviteAsync(AppUser inviter, string loginName, CancellationToken cancellationToken = default)
    {
        if (!inviter.IsHouseholdOwner)
        {
            throw new InvalidOperationException("Only household owners can invite members.");
        }

        if (inviter.LoginName == loginName)
        {
            return;
        }

        var existingUser = await db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.LoginName == loginName, cancellationToken);

        if (existingUser is not null && existingUser.HouseholdId == inviter.HouseholdId)
        {
            return;
        }

        var existingInvite = await db.HouseholdInvites
            .FirstOrDefaultAsync(invite => invite.HouseholdId == inviter.HouseholdId
                && invite.LoginName == loginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null, cancellationToken);

        if (existingInvite is not null)
        {
            return;
        }

        db.HouseholdInvites.Add(new HouseholdInvite
        {
            HouseholdId = inviter.HouseholdId,
            InvitedByUserId = inviter.Id,
            LoginName = loginName,
            CreatedAtUtc = DateTime.UtcNow
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> AcceptInviteAsync(AppUser user, int inviteId, CancellationToken cancellationToken = default)
    {
        if (user.IsHouseholdOwner)
        {
            return false;
        }

        var invite = await db.HouseholdInvites
            .FirstOrDefaultAsync(candidate => candidate.Id == inviteId
                && candidate.LoginName == user.LoginName
                && candidate.AcceptedAtUtc == null
                && candidate.DeclinedAtUtc == null, cancellationToken);

        if (invite is null)
        {
            return false;
        }

        user.HouseholdId = invite.HouseholdId;
        user.IsHouseholdOwner = false;

        var decisionTime = DateTime.UtcNow;
        invite.AcceptedAtUtc = decisionTime;

        var otherPendingInvites = await db.HouseholdInvites
            .Where(candidate => candidate.LoginName == user.LoginName
                && candidate.Id != invite.Id
                && candidate.AcceptedAtUtc == null
                && candidate.DeclinedAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var pendingInvite in otherPendingInvites)
        {
            pendingInvite.DeclinedAtUtc = decisionTime;
        }

        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> DeclineInviteAsync(string loginName, int inviteId, CancellationToken cancellationToken = default)
    {
        var invite = await db.HouseholdInvites
            .FirstOrDefaultAsync(candidate => candidate.Id == inviteId
                && candidate.LoginName == loginName
                && candidate.AcceptedAtUtc == null
                && candidate.DeclinedAtUtc == null, cancellationToken);

        if (invite is null)
        {
            return false;
        }

        invite.DeclinedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
