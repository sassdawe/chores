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
    }

    [Theory]
    [InlineData(Schedule.Daily, 0, AdherenceStatus.OnTime)]     // completed today → due tomorrow
    [InlineData(Schedule.Daily, -1, AdherenceStatus.DueToday)]  // completed yesterday → due today
    [InlineData(Schedule.Daily, -2, AdherenceStatus.Overdue)]   // completed 2 days ago → overdue by 1
    [InlineData(Schedule.Weekly, -6, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Weekly, -7, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Weekly, -8, AdherenceStatus.Overdue)]
    [InlineData(Schedule.BiWeekly, -13, AdherenceStatus.OnTime)]
    [InlineData(Schedule.BiWeekly, -14, AdherenceStatus.DueToday)]
    [InlineData(Schedule.BiWeekly, -15, AdherenceStatus.Overdue)]
    [InlineData(Schedule.Monthly, -29, AdherenceStatus.OnTime)]
    [InlineData(Schedule.Monthly, -30, AdherenceStatus.DueToday)]
    [InlineData(Schedule.Monthly, -31, AdherenceStatus.Overdue)]
    [InlineData(Schedule.EveryTwoDays, -1, AdherenceStatus.OnTime)]
    [InlineData(Schedule.EveryTwoDays, -2, AdherenceStatus.DueToday)]
    [InlineData(Schedule.EveryTwoDays, -3, AdherenceStatus.Overdue)]
    [InlineData(Schedule.TwiceAWeek, -2, AdherenceStatus.OnTime)]
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
    }
}
