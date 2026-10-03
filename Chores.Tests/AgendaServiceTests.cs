using Chores.Models;
using Chores.Services;

namespace Chores.Tests;

public class AgendaServiceTests
{
    private static readonly DateTime NowUtc = new(2026, 3, 10, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Today = NowUtc.Date;

    [Fact]
    public void Build_ProjectsUpcomingOccurrencesWithinHorizon()
    {
        var chore = CreateChore(1, "Vacuum", Schedule.Weekly);
        var latest = Latest(chore, Today.AddDays(-2));

        var agenda = new AgendaService().Build([chore], [], latest, AgendaRange.TwoWeeks, NowUtc);

        var upcomingDates = agenda.Days
            .Where(day => day.Entries.Any(entry => entry.Kind == AgendaEntryKind.Upcoming))
            .Select(day => day.DateUtc)
            .ToArray();

        Assert.Equal([Today.AddDays(5), Today.AddDays(12)], upcomingDates);
    }

    [Theory]
    [InlineData(AgendaRange.OneWeek, 7)]
    [InlineData(AgendaRange.TwoWeeks, 14)]
    [InlineData(AgendaRange.FourWeeks, 28)]
    [InlineData(AgendaRange.EightWeeks, 56)]
    public void Build_HonoursForwardHorizonForEachRange(AgendaRange range, int expectedDays)
    {
        var chore = CreateChore(1, "Dishes", Schedule.Daily);
        var latest = Latest(chore, Today);

        var agenda = new AgendaService().Build([chore], [], latest, range, NowUtc);

        Assert.Equal(Today.AddDays(expectedDays), agenda.HorizonEndUtc);
        Assert.Equal(Today.AddDays(expectedDays), agenda.Days.Max(day => day.DateUtc));
        Assert.Equal(expectedDays, agenda.Days.Count(day => day.Entries.Any(entry => entry.Kind == AgendaEntryKind.Upcoming)));
    }

    [Fact]
    public void Build_PlacesOverdueChoreInAttentionBucketAndProjectsFromToday()
    {
        var chore = CreateChore(1, "Bins", Schedule.Weekly);
        var latest = Latest(chore, Today.AddDays(-20));

        var agenda = new AgendaService().Build([chore], [], latest, AgendaRange.TwoWeeks, NowUtc);

        var overdue = Assert.Single(agenda.Overdue);
        Assert.Equal("Bins", overdue.Chore.Name);
        Assert.Equal(AdherenceStatus.Overdue, overdue.Adherence.Status);

        var upcomingDates = agenda.Days
            .Where(day => day.Entries.Any(entry => entry.Kind == AgendaEntryKind.Upcoming))
            .Select(day => day.DateUtc)
            .ToArray();

        Assert.Equal([Today.AddDays(7), Today.AddDays(14)], upcomingDates);
    }

    [Fact]
    public void Build_ForwardRangeStartsAtTodayAndHidesPastDays()
    {
        var chore = CreateChore(1, "Laundry", Schedule.Weekly);
        var today = CreateRecord(chore, Today);
        var yesterday = CreateRecord(chore, Today.AddDays(-1));
        var lastWeek = CreateRecord(chore, Today.AddDays(-5), isSkipped: true);
        var latest = Latest(chore, Today);

        var agenda = new AgendaService().Build(
            [chore],
            [today, yesterday, lastWeek],
            latest,
            AgendaRange.TwoWeeks,
            NowUtc);

        Assert.Equal(Today, agenda.Days[0].DateUtc);
        Assert.DoesNotContain(agenda.Days, day => day.DateUtc < Today);
        Assert.Equal(today, Assert.Single(agenda.Days[0].Entries).Record);
    }

    [Fact]
    public void Build_PastRangeWalksBackwardsFromTodayAndHidesUpcoming()
    {
        var chore = CreateChore(1, "Laundry", Schedule.Weekly);
        var recent = CreateRecord(chore, Today.AddDays(-3));
        var older = CreateRecord(chore, Today.AddDays(-10));
        var outsideWindow = CreateRecord(chore, Today.AddDays(-400));
        var latest = Latest(chore, Today.AddDays(-3));

        var agenda = new AgendaService().Build(
            [chore],
            [recent, older, outsideWindow],
            latest,
            AgendaRange.PastTwoWeeks,
            NowUtc);

        Assert.Equal(Today, agenda.HorizonEndUtc);
        Assert.DoesNotContain(agenda.Days.SelectMany(day => day.Entries), entry => entry.Kind == AgendaEntryKind.Upcoming);
        Assert.Equal([Today, Today.AddDays(-3), Today.AddDays(-10)], agenda.Days.Select(day => day.DateUtc));
    }

    [Fact]
    public void Build_KeepsAdHocChoresOffTheTimeline()
    {
        var adHoc = CreateChore(1, "Fix shelf", Schedule.AdHoc);
        var latest = Latest(adHoc, null);

        var agenda = new AgendaService().Build([adHoc], [], latest, AgendaRange.FourWeeks, NowUtc);

        Assert.Equal("Fix shelf", Assert.Single(agenda.UnscheduledChores).Name);
        Assert.Empty(agenda.Overdue);
        Assert.DoesNotContain(agenda.Days.SelectMany(day => day.Entries), entry => entry.Kind == AgendaEntryKind.Upcoming);
    }

    [Fact]
    public void Build_AlwaysIncludesTodaySoTheTimelineHasAnAnchor()
    {
        var agenda = new AgendaService().Build([], [], new Dictionary<int, ChoreCompletionAdherence>(), AgendaRange.OneWeek, NowUtc);

        Assert.Equal(Today, agenda.TodayUtc);
        Assert.Equal(Today, Assert.Single(agenda.Days).DateUtc);
    }

    private static Chore CreateChore(int id, string name, Schedule schedule) => new()
    {
        Id = id,
        Name = name,
        Schedule = schedule,
        Household = new Household { Id = 1, Name = "Home" }
    };

    private static CompletionRecord CreateRecord(Chore chore, DateTime completedAtUtc, bool isSkipped = false) => new()
    {
        ChoreId = chore.Id,
        CompletedAtUtc = completedAtUtc,
        IsSkipped = isSkipped,
        CompletedByUser = new AppUser { Id = 1, LoginName = "alice" }
    };

    private static Dictionary<int, ChoreCompletionAdherence> Latest(Chore chore, DateTime? lastCompletedUtc)
    {
        var adherence = new ScheduleAdherenceService().Evaluate(chore.Schedule, lastCompletedUtc, NowUtc);
        return new Dictionary<int, ChoreCompletionAdherence>
        {
            [chore.Id] = new(chore.Id, lastCompletedUtc, adherence)
        };
    }
}
