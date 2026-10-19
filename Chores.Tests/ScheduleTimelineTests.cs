using Chores.Models;
using Chores.Services;

namespace Chores.Tests;

public class ScheduleTimelineTests
{
    private static readonly DateTime Now = new(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetTimeline_AdHocOrNeverDone_ReturnsNull()
    {
        Assert.Null(ScheduleAdherenceService.GetTimeline(Schedule.AdHoc, Now, Now));
        Assert.Null(ScheduleAdherenceService.GetTimeline(Schedule.Weekly, null, Now));
    }

    [Fact]
    public void GetTimeline_ZonesSumToHundredAndMarkerTracksElapsedDays()
    {
        var t = ScheduleAdherenceService.GetTimeline(Schedule.Weekly, Now.AddDays(-5), Now)!;
        Assert.Equal(100, t.GreenPercent + t.YellowPercent + t.RedPercent, 6);
        Assert.Equal(5 / 10.0 * 100, t.MarkerPercent, 6);
    }

    [Fact]
    public void GetTimeline_MarkerIsClamped()
    {
        var t = ScheduleAdherenceService.GetTimeline(Schedule.Daily, Now.AddDays(-30), Now)!;
        Assert.Equal(100, t.MarkerPercent);
    }
}
