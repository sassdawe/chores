using Chores.Data;
using Chores.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public class HouseholdInvitationService(AppDbContext db)
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(5);

    public async Task<List<HouseholdInvite>> GetPendingInvitesAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        var registrationCutoffUtc = await GetRegistrationCutoffUtcAsync(user, cancellationToken);
        if (registrationCutoffUtc is null)
        {
            return [];
        }

        await DiscardObsoletePendingInvitesAsync(user.LoginName, registrationCutoffUtc.Value, cancellationToken);

        return await db.HouseholdInvites
            .AsNoTracking()
            .Include(invite => invite.Household)
            .Include(invite => invite.InvitedByUser)
            .Where(invite => invite.LoginName == user.LoginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null)
            .Where(invite => invite.CreatedAtUtc >= registrationCutoffUtc.Value)
            .OrderByDescending(invite => invite.CreatedAtUtc)
            .ToListAsync(cancellationToken);
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
            .Include(user => user.Credentials)
            .FirstOrDefaultAsync(user => user.LoginName == loginName, cancellationToken);

        if (existingUser is not null && existingUser.HouseholdId == inviter.HouseholdId)
        {
            return;
        }

        var registrationCutoffUtc = await GetRegistrationCutoffUtcAsync(existingUser, cancellationToken);
        await DiscardObsoletePendingInvitesAsync(loginName, registrationCutoffUtc, cancellationToken);

        var existingInvite = await db.HouseholdInvites
            .FirstOrDefaultAsync(invite => invite.HouseholdId == inviter.HouseholdId
                && invite.LoginName == loginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null
                && invite.CreatedAtUtc >= GetActiveInviteCutoffUtc(registrationCutoffUtc), cancellationToken);

        if (existingInvite is not null)
        {
            return;
        }

        var invite = new HouseholdInvite
        {
            HouseholdId = inviter.HouseholdId,
            InvitedByUserId = inviter.Id,
            LoginName = loginName,
            CreatedAtUtc = DateTime.UtcNow
        };
        db.HouseholdInvites.Add(invite);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsPendingInviteConflict(ex))
        {
            db.Entry(invite).State = EntityState.Detached;
        }
    }

    public async Task<bool> AcceptInviteAsync(AppUser user, int inviteId, CancellationToken cancellationToken = default)
    {
        if (!await CanAcceptInvitesAsync(user, cancellationToken))
        {
            return false;
        }

        var registrationCutoffUtc = await GetRegistrationCutoffUtcAsync(user, cancellationToken);
        if (registrationCutoffUtc is null)
        {
            return false;
        }

        await DiscardObsoletePendingInvitesAsync(user.LoginName, registrationCutoffUtc.Value, cancellationToken);

        var invite = await db.HouseholdInvites
            .FirstOrDefaultAsync(candidate => candidate.Id == inviteId
                && candidate.LoginName == user.LoginName
                && candidate.AcceptedAtUtc == null
                && candidate.DeclinedAtUtc == null
                && candidate.CreatedAtUtc >= registrationCutoffUtc.Value, cancellationToken);

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

    public async Task<bool> CanAcceptInvitesAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        if (!user.IsHouseholdOwner)
        {
            return true;
        }

        return await IsOnlyMemberOfEmptyHouseholdAsync(user, cancellationToken);
    }

    public async Task<bool> DeclineInviteAsync(string loginName, int inviteId, CancellationToken cancellationToken = default)
    {
        var existingUser = await db.Users
            .AsNoTracking()
            .Include(user => user.Credentials)
            .FirstOrDefaultAsync(user => user.LoginName == loginName, cancellationToken);

        var registrationCutoffUtc = await GetRegistrationCutoffUtcAsync(existingUser, cancellationToken);
        await DiscardObsoletePendingInvitesAsync(loginName, registrationCutoffUtc, cancellationToken);

        var invite = await db.HouseholdInvites
            .FirstOrDefaultAsync(candidate => candidate.Id == inviteId
                && candidate.LoginName == loginName
                && candidate.AcceptedAtUtc == null
                && candidate.DeclinedAtUtc == null
                && candidate.CreatedAtUtc >= GetActiveInviteCutoffUtc(registrationCutoffUtc), cancellationToken);

        if (invite is null)
        {
            return false;
        }

        invite.DeclinedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<DateTime?> GetRegistrationCutoffUtcAsync(AppUser? user, CancellationToken cancellationToken)
    {
        if (user is null)
        {
            return null;
        }

        var firstCredential = user.Credentials.OrderBy(credential => credential.RegDate).FirstOrDefault();
        if (firstCredential is not null)
        {
            return firstCredential.RegDate;
        }

        return await db.FidoCredentials
            .Where(credential => credential.UserId == user.Id)
            .OrderBy(credential => credential.RegDate)
            .Select(credential => (DateTime?)credential.RegDate)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<bool> IsOnlyMemberOfEmptyHouseholdAsync(AppUser user, CancellationToken cancellationToken)
    {
        var hasOtherMembers = await db.Users
            .AnyAsync(candidate => candidate.HouseholdId == user.HouseholdId && candidate.Id != user.Id, cancellationToken);
        if (hasOtherMembers)
        {
            return false;
        }

        var hasChores = await db.Chores.AnyAsync(chore => chore.HouseholdId == user.HouseholdId, cancellationToken);
        var hasLabels = await db.Labels.AnyAsync(label => label.HouseholdId == user.HouseholdId, cancellationToken);
        var hasInvites = await db.HouseholdInvites.AnyAsync(invite => invite.HouseholdId == user.HouseholdId, cancellationToken);

        return !hasChores && !hasLabels && !hasInvites;
    }

    private static DateTime GetActiveInviteCutoffUtc(DateTime? registrationCutoffUtc)
    {
        var expirationCutoffUtc = DateTime.UtcNow - InviteLifetime;
        if (registrationCutoffUtc is null)
        {
            return expirationCutoffUtc;
        }

        return registrationCutoffUtc.Value > expirationCutoffUtc
            ? registrationCutoffUtc.Value
            : expirationCutoffUtc;
    }

    private async Task DiscardObsoletePendingInvitesAsync(string loginName, DateTime? registrationCutoffUtc, CancellationToken cancellationToken)
    {
        var activeInviteCutoffUtc = GetActiveInviteCutoffUtc(registrationCutoffUtc);

        var obsoleteInvites = await db.HouseholdInvites
            .Where(invite => invite.LoginName == loginName
                && invite.AcceptedAtUtc == null
                && invite.DeclinedAtUtc == null
                && invite.CreatedAtUtc < activeInviteCutoffUtc)
            .ToListAsync(cancellationToken);

        if (obsoleteInvites.Count == 0)
        {
            return;
        }

        db.HouseholdInvites.RemoveRange(obsoleteInvites);
        await db.SaveChangesAsync(cancellationToken);
    }

    private static bool IsPendingInviteConflict(DbUpdateException exception)
    {
        return exception.InnerException is SqliteException sqliteException
            && sqliteException.SqliteErrorCode == 19
            && sqliteException.Message.Contains("IX_HouseholdInvites_PendingHouseholdLoginName", StringComparison.Ordinal);
    }
}
