using Chores.Data;
using Chores.Models;
using Microsoft.EntityFrameworkCore;

namespace Chores.Services;

public record ChoreCompletionAdherence(int ChoreId, DateTime? LastCompletedAtUtc, ScheduleAdherence Adherence);

public static class ChoreCompletionAdherenceQuery
{
    public static async Task<Dictionary<int, ChoreCompletionAdherence>> GetLatestByChoreAsync(
        AppDbContext db,
        IReadOnlyCollection<Chore> chores,
        ScheduleAdherenceService adherence,
        CancellationToken cancellationToken = default)
    {
        if (chores.Count == 0)
        {
            return [];
        }

        var choreIds = chores.Select(chore => chore.Id).ToList();
        var latestByChore = await db.CompletionRecords
            .Where(record => choreIds.Contains(record.ChoreId))
            .GroupBy(record => record.ChoreId)
            .Select(group => new
            {
                ChoreId = group.Key,
                LastCompletedAtUtc = group.Max(record => record.CompletedAtUtc)
            })
            .ToDictionaryAsync(item => item.ChoreId, item => (DateTime?)item.LastCompletedAtUtc, cancellationToken);

        return chores.ToDictionary(
            chore => chore.Id,
            chore =>
            {
                latestByChore.TryGetValue(chore.Id, out var lastCompletedAtUtc);
                var scheduleAdherence = adherence.Evaluate(chore.Schedule, lastCompletedAtUtc);
                return new ChoreCompletionAdherence(chore.Id, lastCompletedAtUtc, scheduleAdherence);
            });
    }
}
