using Chores.Models;

namespace Chores.Services;

public enum AdherenceStatus { OnTime, DueSoon, DueToday, Overdue }

public record ScheduleAdherence(AdherenceStatus Status, int DaysOverdue, int? DaysUntilDue);

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
            return new ScheduleAdherence(AdherenceStatus.Overdue, int.MaxValue, null);

        var lastDate = lastCompletedUtc.Value.Date;
        var intervalDays = IntervalDays(schedule);
        var nextDueDate = lastDate.AddDays(intervalDays);
        var daysOverdue = (now - nextDueDate).Days;
        var daysUntilDue = (nextDueDate - now).Days;

        return daysOverdue switch
        {
            < 0 when daysUntilDue <= NotificationLeadDays(schedule)
                => new ScheduleAdherence(AdherenceStatus.DueSoon, 0, daysUntilDue),
            < 0 => new ScheduleAdherence(AdherenceStatus.OnTime, 0, daysUntilDue),
            0 => new ScheduleAdherence(AdherenceStatus.DueToday, 0, 0),
            _ => new ScheduleAdherence(AdherenceStatus.Overdue, daysOverdue, 0)
        };
    }

    public static string ToDisplayText(Schedule schedule, ScheduleAdherence adherence)
    {
        return adherence.Status switch
        {
            AdherenceStatus.OnTime => "On time",
            AdherenceStatus.DueSoon when adherence.DaysUntilDue is 1 => "Due tomorrow",
            AdherenceStatus.DueSoon when schedule == Schedule.Monthly && adherence.DaysUntilDue <= 7 => "Due this week",
            AdherenceStatus.DueSoon when adherence.DaysUntilDue is > 1 => $"Due in {adherence.DaysUntilDue} days",
            AdherenceStatus.DueToday => "Due today",
            AdherenceStatus.Overdue when adherence.DaysOverdue == int.MaxValue => "Never done",
            AdherenceStatus.Overdue => $"{adherence.DaysOverdue} days overdue",
            _ => "On time"
        };
    }

    public static string ToBadgeClass(Schedule schedule, ScheduleAdherence adherence)
    {
        return adherence.Status switch
        {
            AdherenceStatus.OnTime => "text-bg-success",
            AdherenceStatus.DueSoon when adherence.DaysUntilDue is <= 1 => "text-bg-warning",
            AdherenceStatus.DueSoon when adherence.DaysUntilDue is <= 3 => "text-bg-primary",
            AdherenceStatus.DueSoon => "text-bg-info",
            AdherenceStatus.DueToday => "text-bg-due-today",
            AdherenceStatus.Overdue => "text-bg-danger",
            _ => "text-bg-success"
        };
    }

    private static int NotificationLeadDays(Schedule schedule) => schedule switch
    {
        Schedule.Daily => 0,
        Schedule.EveryTwoDays => 1,
        Schedule.TwiceAWeek => 1,
        Schedule.Weekly => 3,
        Schedule.BiWeekly => 5,
        Schedule.Monthly => 7,
        Schedule.Quarterly => 14,
        Schedule.EverySixMonths => 21,
        Schedule.Yearly => 30,
        Schedule.EveryTwoYears => 45,
        _ => throw new ArgumentOutOfRangeException(nameof(schedule), schedule, null)
    };

    private static int IntervalDays(Schedule schedule) => schedule switch
    {
        Schedule.Daily => 1,
        Schedule.TwiceAWeek => 3,   // roughly every 3–4 days
        Schedule.EveryTwoDays => 2,
        Schedule.Weekly => 7,
        Schedule.BiWeekly => 14,
        Schedule.Monthly => 30,
        Schedule.Quarterly => 91,
        Schedule.EverySixMonths => 182,
        Schedule.Yearly => 365,
        Schedule.EveryTwoYears => 730,
        _ => throw new ArgumentOutOfRangeException(nameof(schedule), schedule, null)
    };
}
