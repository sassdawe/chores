using Chores.Models;
using Chores.Services;

namespace Chores.Tests;

public class ScheduleAdherenceServiceTests
{
    private readonly ScheduleAdherenceService _sut = new();
    private static readonly DateTime Now = new(2025, 6, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void NeverCompleted_ReturnsOverdue()
    {
        var result = _sut.Evaluate(Schedule.Weekly, null, Now);
        Assert.Equal(AdherenceStatus.Overdue, result.Status);
        Assert.Equal(int.MaxValue, result.DaysOverdue);
        Assert.Null(result.DaysUntilDue);
    }

    [Theory]
    [InlineData(Schedule.Daily, 0, AdherenceStatus.OnTime)]     // completed today → due tomorrow
    [InlineData(Schedule.Daily, -1, AdherenceStatus.DueToday)]  // completed yesterday → due today
    [InlineData(Schedule.Daily, -2, AdherenceStatus.Overdue)]   // completed 2 days ago → overdue by 1
    [InlineData(Schedule.Weekly, -3, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Weekly, -6, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.Weekly, -7, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Weekly, -8, AdherenceStatus.Overdue)]
    [InlineData(Schedule.BiWeekly, -8, AdherenceStatus.OnTime)]
    [InlineData(Schedule.BiWeekly, -13, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.BiWeekly, -14, AdherenceStatus.DueToday)]
    [InlineData(Schedule.BiWeekly, -15, AdherenceStatus.Overdue)]
    [InlineData(Schedule.Monthly, -20, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Monthly, -29, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.Monthly, -30, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Monthly, -31, AdherenceStatus.Overdue)]
    [InlineData(Schedule.Quarterly, -76, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Quarterly, -77, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.Quarterly, -91, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Quarterly, -92, AdherenceStatus.Overdue)]
    [InlineData(Schedule.EverySixMonths, -160, AdherenceStatus.OnTime)]
    [InlineData(Schedule.EverySixMonths, -161, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.EverySixMonths, -182, AdherenceStatus.DueToday)]
    [InlineData(Schedule.EverySixMonths, -183, AdherenceStatus.Overdue)]
    [InlineData(Schedule.Yearly, -334, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Yearly, -335, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.Yearly, -365, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Yearly, -366, AdherenceStatus.Overdue)]
    [InlineData(Schedule.EveryTwoYears, -684, AdherenceStatus.OnTime)]
    [InlineData(Schedule.EveryTwoYears, -685, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.EveryTwoYears, -730, AdherenceStatus.DueToday)]
    [InlineData(Schedule.EveryTwoYears, -731, AdherenceStatus.Overdue)]
    [InlineData(Schedule.EveryTwoDays, 0, AdherenceStatus.OnTime)]
    [InlineData(Schedule.EveryTwoDays, -1, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.EveryTwoDays, -2, AdherenceStatus.DueToday)]
    [InlineData(Schedule.EveryTwoDays, -3, AdherenceStatus.Overdue)]
    [InlineData(Schedule.TwiceAWeek, -1, AdherenceStatus.OnTime)]
    [InlineData(Schedule.TwiceAWeek, -2, AdherenceStatus.DueSoon)]
    [InlineData(Schedule.TwiceAWeek, -3, AdherenceStatus.DueToday)]
    [InlineData(Schedule.TwiceAWeek, -4, AdherenceStatus.Overdue)]
    public void ScheduleAdherence_VariousOffsets(Schedule schedule, int daysAgo, AdherenceStatus expected)
    {
        var lastCompleted = Now.AddDays(daysAgo);
        var result = _sut.Evaluate(schedule, lastCompleted, Now);
        Assert.Equal(expected, result.Status);
    }

    [Fact]
    public void Overdue_ReturnsCorrectDaysOverdueCount()
    {
        var lastCompleted = Now.AddDays(-10); // weekly = 7 day interval, so 3 days overdue
        var result = _sut.Evaluate(Schedule.Weekly, lastCompleted, Now);
        Assert.Equal(AdherenceStatus.Overdue, result.Status);
        Assert.Equal(3, result.DaysOverdue);
    }

    [Fact]
    public void OnTime_DaysOverdueIsZero()
    {
        var lastCompleted = Now.AddDays(-3);
        var result = _sut.Evaluate(Schedule.Weekly, lastCompleted, Now);
        Assert.Equal(AdherenceStatus.OnTime, result.Status);
        Assert.Equal(0, result.DaysOverdue);
        Assert.Equal(4, result.DaysUntilDue);
    }

    [Theory]
    [InlineData(Schedule.Weekly, -4, AdherenceStatus.DueSoon, 3)]
    [InlineData(Schedule.BiWeekly, -9, AdherenceStatus.DueSoon, 5)]
    [InlineData(Schedule.Monthly, -23, AdherenceStatus.DueSoon, 7)]
    [InlineData(Schedule.Quarterly, -77, AdherenceStatus.DueSoon, 14)]
    [InlineData(Schedule.EverySixMonths, -161, AdherenceStatus.DueSoon, 21)]
    [InlineData(Schedule.Yearly, -335, AdherenceStatus.DueSoon, 30)]
    [InlineData(Schedule.EveryTwoYears, -685, AdherenceStatus.DueSoon, 45)]
    public void Evaluate_UsesScheduleRelativeLeadTime(Schedule schedule, int daysAgo, AdherenceStatus expectedStatus, int expectedDaysUntilDue)
    {
        var lastCompleted = Now.AddDays(daysAgo);
        var result = _sut.Evaluate(schedule, lastCompleted, Now);

        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(expectedDaysUntilDue, result.DaysUntilDue);
        Assert.Equal(0, result.DaysOverdue);
    }

    [Theory]
    [InlineData(Schedule.Weekly, -4, "Due in 3 days", "text-bg-primary")]
    [InlineData(Schedule.BiWeekly, -9, "Due in 5 days", "text-bg-info")]
    [InlineData(Schedule.Monthly, -23, "Due this week", "text-bg-info")]
    [InlineData(Schedule.Quarterly, -77, "Due in 14 days", "text-bg-info")]
    [InlineData(Schedule.EverySixMonths, -161, "Due in 21 days", "text-bg-info")]
    [InlineData(Schedule.Yearly, -335, "Due in 30 days", "text-bg-info")]
    [InlineData(Schedule.EveryTwoYears, -685, "Due in 45 days", "text-bg-info")]
    [InlineData(Schedule.Weekly, -6, "Due tomorrow", "text-bg-warning")]
    public void DisplayHelpers_ReturnExpectedNotificationTextAndBadge(Schedule schedule, int daysAgo, string expectedText, string expectedBadgeClass)
    {
        var adherence = _sut.Evaluate(schedule, Now.AddDays(daysAgo), Now);

        Assert.Equal(expectedText, ScheduleAdherenceService.ToDisplayText(schedule, adherence));
        Assert.Equal(expectedBadgeClass, ScheduleAdherenceService.ToBadgeClass(schedule, adherence));
    }

    [Fact]
    public void DisplayHelpers_DueToday_UsesDedicatedUrgencyColor()
    {
        var adherence = _sut.Evaluate(Schedule.Weekly, Now.AddDays(-7), Now);

        Assert.Equal(AdherenceStatus.DueToday, adherence.Status);
        Assert.Equal("text-bg-due-today", ScheduleAdherenceService.ToBadgeClass(Schedule.Weekly, adherence));
    }
}
