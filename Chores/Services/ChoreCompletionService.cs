using Chores.Data;
using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public enum ChoreCompletionOutcome
{
    /// <summary>A new completion record was appended.</summary>
    Recorded,

    /// <summary>A record with the same client record id already exists; nothing was appended.</summary>
    Duplicate,

    /// <summary>The user or chore does not exist, or the user cannot access the chore's space.</summary>
    NotFound
}

/// <summary>
/// Single place where completion records are appended, shared by the server-rendered confirmation
/// page and the offline sync endpoint so both apply identical access checks and clamping.
/// </summary>
public class ChoreCompletionService
{
    /// <summary>Upper bound on how many queued completions a single sync request may carry.</summary>
    public const int MaxBatchSize = 200;

    private readonly AppDbContext _db;
    private readonly HouseholdMembershipService _householdMemberships;

    public ChoreCompletionService(AppDbContext db, HouseholdMembershipService householdMemberships)
    {
        _db = db;
        _householdMemberships = householdMemberships;
    }

    public async Task<ChoreCompletionOutcome> RecordAsync(
        string? loginName,
        int choreId,
        DateTime completedAtUtc,
        bool isSkipped = false,
        string? clientRecordId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.LoginName == loginName, cancellationToken);
        if (user is null) return ChoreCompletionOutcome.NotFound;

        var chore = await _db.Chores.FirstOrDefaultAsync(c => c.Id == choreId, cancellationToken);
        if (chore is null) return ChoreCompletionOutcome.NotFound;
        if (!await _householdMemberships.CanAccessHouseholdAsync(user.LoginName, chore.HouseholdId)) return ChoreCompletionOutcome.NotFound;

        completedAtUtc = NormalizeToUtc(completedAtUtc);

        if (clientRecordId is not null
            && await _db.CompletionRecords.AnyAsync(r => r.ClientRecordId == clientRecordId, cancellationToken))
        {
            return ChoreCompletionOutcome.Duplicate;
        }

        var entry = _db.CompletionRecords.Add(new CompletionRecord
        {
            ChoreId = chore.Id,
            CompletedByUserId = user.Id,
            CompletedAtUtc = completedAtUtc,
            IsSkipped = isSkipped,
            ClientRecordId = clientRecordId
        });

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException) when (clientRecordId is not null)
        {
            // Lost a race against a concurrent sync of the same queued completion -- but only treat
            // it as a duplicate if the winning row is really there, so genuine write failures still
            // surface instead of being reported to the client as "already recorded".
            entry.State = EntityState.Detached;

            if (await _db.CompletionRecords.AnyAsync(r => r.ClientRecordId == clientRecordId, cancellationToken))
            {
                return ChoreCompletionOutcome.Duplicate;
            }

            throw;
        }

        return ChoreCompletionOutcome.Recorded;
    }

    /// <summary>
    /// Treats unspecified timestamps as UTC and clamps the future away: an offline device with a
    /// skewed clock must not be able to park a completion ahead of the server's "now".
    /// </summary>
    private static DateTime NormalizeToUtc(DateTime completedAt)
    {
        var utc = completedAt.Kind switch
        {
            DateTimeKind.Local => completedAt.ToUniversalTime(),
            DateTimeKind.Utc => completedAt,
            _ => DateTime.SpecifyKind(completedAt, DateTimeKind.Utc)
        };

        var now = DateTime.UtcNow;
        return utc > now ? now : utc;
    }
}
