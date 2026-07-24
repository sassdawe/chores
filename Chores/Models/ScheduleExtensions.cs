namespace Chores.Models;

public static class ScheduleExtensions
{
    public static IReadOnlyList<Schedule> GetSelectableSchedules() =>
    [
        Schedule.AdHoc,
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
        Schedule.AdHoc => "Ad-hoc",
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

    /// <summary>
    /// Returns the schedule label translated via the provided <see cref="Chores.Services.UiTranslationService"/>.
    /// Falls back to <see cref="ToFriendlyLabel"/> if the service is null.
    /// </summary>
    public static string ToLocalizedLabel(this Schedule schedule, Chores.Services.UiTranslationService? t)
    {
        if (t is null)
            return schedule.ToFriendlyLabel();

        var key = schedule switch
        {
            Schedule.AdHoc => "schedule.adHoc",
            Schedule.Daily => "schedule.daily",
            Schedule.TwiceAWeek => "schedule.twiceAWeek",
            Schedule.EveryTwoDays => "schedule.everyTwoDays",
            Schedule.EveryThreeDays => "schedule.everyThreeDays",
            Schedule.Weekly => "schedule.weekly",
            Schedule.BiWeekly => "schedule.biWeekly",
            Schedule.EveryThreeWeeks => "schedule.everyThreeWeeks",
            Schedule.Monthly => "schedule.monthly",
            Schedule.BiMonthly => "schedule.biMonthly",
            Schedule.Quarterly => "schedule.quarterly",
            Schedule.EverySixMonths => "schedule.everySixMonths",
            Schedule.Yearly => "schedule.yearly",
            Schedule.EveryTwoYears => "schedule.everyTwoYears",
            _ => null
        };

        return key is null ? schedule.ToFriendlyLabel() : t[key];
    }
}
