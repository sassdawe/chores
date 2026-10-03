using Chores.Models;

namespace Chores.Services;

public enum AgendaRange
{
    OneWeek,
    TwoWeeks,
    FourWeeks,
    EightWeeks,
    PastTwoWeeks
}

public enum AgendaEntryKind
{
    Completed,
    Skipped,
    Upcoming
}

public record AgendaEntry(Chore Chore, AgendaEntryKind Kind, CompletionRecord? Record);

public record AgendaDay(DateTime DateUtc, IReadOnlyList<AgendaEntry> Entries);

public record AgendaOverdueEntry(Chore Chore, ScheduleAdherence Adherence);

public record Agenda(
    DateTime TodayUtc,
    DateTime HorizonEndUtc,
    IReadOnlyList<AgendaOverdueEntry> Overdue,
    IReadOnlyList<AgendaDay> Days,
    IReadOnlyList<Chore> UnscheduledChores);

/// <summary>
/// Builds the scrolling agenda timeline. Forward ranges start at today and project upcoming
/// occurrences; the past range instead walks backwards through recorded completions.
/// Pure and EF-free so it can be unit-tested directly.
/// </summary>
public class AgendaService
{
    public const int PastWindowDays = 14;
    public const int MaxPastRecords = 500;

    public static int ForwardWeeks(AgendaRange range) => range switch
    {
        AgendaRange.OneWeek => 1,
        AgendaRange.TwoWeeks => 2,
        AgendaRange.FourWeeks => 4,
        AgendaRange.EightWeeks => 8,
        _ => 0
    };

    /// <summary>Earliest completion date the range can display; shared with the database query so both agree.</summary>
    public static DateTime PastStartUtc(AgendaRange range, DateTime todayUtc) =>
        range == AgendaRange.PastTwoWeeks ? todayUtc.AddDays(1 - PastWindowDays) : todayUtc;

    /// <param name="chores">Chores already filtered by space and label.</param>
    /// <param name="records">Completion records for those chores, already limited to the requested window.</param>
    /// <param name="latestByChore">Latest completion and adherence per chore, including completions older than the window.</param>
    public Agenda Build(
        IReadOnlyCollection<Chore> chores,
        IReadOnlyCollection<CompletionRecord> records,
        IReadOnlyDictionary<int, ChoreCompletionAdherence> latestByChore,
        AgendaRange range,
        DateTime nowUtc)
    {
        var today = nowUtc.Date;
        var isPastView = range == AgendaRange.PastTwoWeeks;
        var pastStart = PastStartUtc(range, today);
        var horizonEnd = today.AddDays(7 * ForwardWeeks(range));

        var choreById = chores.ToDictionary(chore => chore.Id);
        var entriesByDate = new Dictionary<DateTime, List<AgendaEntry>>
        {
            [today] = []
        };

        foreach (var record in records)
        {
            if (!choreById.TryGetValue(record.ChoreId, out var chore))
                continue;

            var date = record.CompletedAtUtc.Date;
            if (date < pastStart || date > today)
                continue;

            var kind = record.IsSkipped ? AgendaEntryKind.Skipped : AgendaEntryKind.Completed;
            AddEntry(entriesByDate, date, new AgendaEntry(chore, kind, record));
        }

        var overdue = new List<AgendaOverdueEntry>();
        var unscheduled = new List<Chore>();

        foreach (var chore in chores)
        {
            var intervalDays = ScheduleAdherenceService.GetIntervalDays(chore.Schedule);
            if (intervalDays is null)
            {
                unscheduled.Add(chore);
                continue;
            }

            latestByChore.TryGetValue(chore.Id, out var latest);
            var adherence = latest?.Adherence;
            if (adherence is { Status: AdherenceStatus.Overdue })
            {
                overdue.Add(new AgendaOverdueEntry(chore, adherence));
            }

            if (isPastView)
                continue;

            var lastCompleted = latest?.LastCompletedAtUtc?.Date;
            var nextDue = lastCompleted?.AddDays(intervalDays.Value) ?? today.AddDays(intervalDays.Value);

            // An overdue chore is projected as if it were done today, so the timeline stays forward-looking.
            if (nextDue < today)
                nextDue = today.AddDays(intervalDays.Value);

            for (var occurrence = nextDue; occurrence <= horizonEnd; occurrence = occurrence.AddDays(intervalDays.Value))
            {
                AddEntry(entriesByDate, occurrence, new AgendaEntry(chore, AgendaEntryKind.Upcoming, null));
            }
        }

        var orderedDates = isPastView
            ? entriesByDate.Keys.OrderByDescending(date => date)
            : entriesByDate.Keys.OrderBy(date => date);

        var days = orderedDates
            .Select(date => new AgendaDay(date, SortEntries(entriesByDate[date])))
            .ToList();

        return new Agenda(
            today,
            horizonEnd,
            [.. overdue.OrderByDescending(entry => entry.Adherence.DaysOverdue)
                .ThenBy(entry => entry.Chore.Name, StringComparer.CurrentCultureIgnoreCase)],
            days,
            [.. unscheduled.OrderBy(chore => chore.Name, StringComparer.CurrentCultureIgnoreCase)]);
    }

    private static void AddEntry(Dictionary<DateTime, List<AgendaEntry>> entriesByDate, DateTime date, AgendaEntry entry)
    {
        if (!entriesByDate.TryGetValue(date, out var entries))
        {
            entries = [];
            entriesByDate[date] = entries;
        }

        entries.Add(entry);
    }

    private static List<AgendaEntry> SortEntries(List<AgendaEntry> entries)
    {
        return [.. entries
            .OrderBy(entry => entry.Kind)
            .ThenByDescending(entry => entry.Record?.CompletedAtUtc ?? DateTime.MinValue)
            .ThenBy(entry => entry.Chore.Name, StringComparer.CurrentCultureIgnoreCase)];
    }
}
