namespace Chores.Models;

public static class ScheduleExtensions
{
    public static string ToFriendlyLabel(this Schedule schedule) => schedule switch
    {
        Schedule.Daily => "Daily",
        Schedule.TwiceAWeek => "Twice a week",
        Schedule.EveryTwoDays => "Every two days",
        Schedule.Weekly => "Weekly",
        Schedule.BiWeekly => "Bi-weekly",
        Schedule.Monthly => "Monthly",
        Schedule.Quarterly => "Quarterly",
        Schedule.EverySixMonths => "Every 6 months",
        Schedule.Yearly => "Yearly",
        Schedule.EveryTwoYears => "Every 2 years",
        _ => schedule.ToString()
    };
}
