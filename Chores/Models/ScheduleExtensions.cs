namespace Chores.Models;

public static class ScheduleExtensions
{
    public static IReadOnlyList<Schedule> GetSelectableSchedules() =>
    [
        Schedule.Daily,
        Schedule.TwiceAWeek,
        Schedule.EveryTwoDays,
        Schedule.EveryThreeDays,
        Schedule.Weekly,
        Schedule.BiWeekly,
        Schedule.EveryThreeWeeks,
        Schedule.Monthly,
        Schedule.BiMonthly,
        Schedule.Quarterly,
        Schedule.EverySixMonths,
        Schedule.Yearly,
        Schedule.EveryTwoYears
    ];

    public static string ToFriendlyLabel(this Schedule schedule) => schedule switch
    {
        Schedule.Daily => "Daily",
        Schedule.TwiceAWeek => "Twice a week",
        Schedule.EveryTwoDays => "Every two days",
        Schedule.EveryThreeDays => "Every three days",
        Schedule.Weekly => "Weekly",
        Schedule.BiWeekly => "Bi-weekly",
        Schedule.EveryThreeWeeks => "Every three weeks",
        Schedule.Monthly => "Monthly",
        Schedule.BiMonthly => "Bi-monthly",
        Schedule.Quarterly => "Quarterly",
        Schedule.EverySixMonths => "Every 6 months",
        Schedule.Yearly => "Yearly",
        Schedule.EveryTwoYears => "Every 2 years",
        _ => schedule.ToString()
    };
}
