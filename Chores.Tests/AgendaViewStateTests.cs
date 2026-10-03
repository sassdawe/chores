using Chores.Services;

namespace Chores.Tests;

public class AgendaViewStateTests
{
    [Fact]
    public void CookieName_IsStablePerLoginNameAndHidesIt()
    {
        var name = AgendaViewState.CookieName("alice");

        Assert.Equal(name, AgendaViewState.CookieName("alice"));
        Assert.NotEqual(name, AgendaViewState.CookieName("bob"));
        Assert.StartsWith(AgendaViewState.CookiePrefix, name);
        Assert.DoesNotContain("alice", name, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_ReadsOpenAndClosedFlags()
    {
        var states = AgendaViewState.Parse("overdue:0|d20260310:1|adhoc:0");

        Assert.False(states["overdue"]);
        Assert.True(states["d20260310"]);
        Assert.False(states["adhoc"]);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("overdue")]
    [InlineData("overdue:2")]
    [InlineData(":1")]
    public void Parse_IgnoresMalformedInput(string? cookieValue)
    {
        Assert.Empty(AgendaViewState.Parse(cookieValue));
    }

    [Fact]
    public void Parse_IgnoresOversizedCookie()
    {
        var oversized = string.Join('|', Enumerable.Repeat("d20260310:1", 500));

        Assert.True(oversized.Length > AgendaViewState.MaxCookieLength);
        Assert.Empty(AgendaViewState.Parse(oversized));
    }

    [Fact]
    public void DayKey_UsesSortableUtcDate()
    {
        Assert.Equal("d20260310", AgendaViewState.DayKey(new DateTime(2026, 3, 10, 23, 30, 0, DateTimeKind.Utc)));
    }
}
