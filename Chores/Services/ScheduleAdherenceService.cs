using Chores.Models;

namespace Chores.Services;

public enum AdherenceStatus { OnTime, DueToday, Overdue }

public record ScheduleAdherence(AdherenceStatus Status, int DaysOverdue);

/// <summary>
/// Pure, stateless service that computes how a chore is tracking against its schedule.
/// Has no EF / infrastructure dependency — safe to unit-test directly.
/// </summary>
public class ScheduleAdherenceService
{
    /// <param name="schedule">The chore's configured repetition schedule.</param>
    /// <param name="lastCompletedUtc">UTC timestamp of the most recent completion, or null if never done.</param>
    /// <param name="nowUtc">Current UTC time (injected for testability).</param>
    public ScheduleAdherence Evaluate(Schedule schedule, DateTime? lastCompletedUtc, DateTime? nowUtc = null)
    {
        var now = (nowUtc ?? DateTime.UtcNow).Date;

        if (lastCompletedUtc is null)
            return new ScheduleAdherence(AdherenceStatus.Overdue, int.MaxValue);

        var lastDate = lastCompletedUtc.Value.Date;
        var intervalDays = IntervalDays(schedule);
        var nextDueDate = lastDate.AddDays(intervalDays);
        var daysOverdue = (now - nextDueDate).Days;

        return daysOverdue switch
        {
            < 0 => new ScheduleAdherence(AdherenceStatus.OnTime, 0),
            0 => new ScheduleAdherence(AdherenceStatus.DueToday, 0),
            _ => new ScheduleAdherence(AdherenceStatus.Overdue, daysOverdue)
        };
    }

    private static int IntervalDays(Schedule schedule) => schedule switch
    {
        Schedule.Daily => 1,
        Schedule.TwiceAWeek => 3,   // roughly every 3–4 days
        Schedule.EveryTwoDays => 2,
        Schedule.Weekly => 7,
        Schedule.BiWeekly => 14,
        Schedule.Monthly => 30,
        _ => throw new ArgumentOutOfRangeException(nameof(schedule), schedule, null)
    };
}
