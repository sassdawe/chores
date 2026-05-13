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
        _ => schedule.ToString()
    };
}
