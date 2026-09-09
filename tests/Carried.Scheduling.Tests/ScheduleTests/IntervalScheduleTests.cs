using Carried.Scheduling.Schedule;

namespace Carried.Scheduling.Tests.ScheduleTests;

public sealed class IntervalScheduleTests
{
    private static readonly DateTimeOffset Anchor =
        DateTimeOffset.Parse("2026-09-09T12:00:00Z");

    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    [Fact]
    public void GetNextOccurrence_BeforeAnchor_ReturnsFirstOccurrence()
    {
        var schedule = new IntervalSchedule(Interval, Anchor);
        var after = DateTimeOffset.Parse("2026-09-09T11:30:00Z");

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Equal(Anchor + Interval, result);
    }

    [Fact]
    public void GetNextOccurrence_AtAnchor_ReturnsFirstOccurrence()
    {
        var schedule = new IntervalSchedule(Interval, Anchor);

        DateTimeOffset? result = schedule.GetNextOccurrence(Anchor);

        Assert.Equal(Anchor + Interval, result);
    }

    [Fact]
    public void GetNextOccurrence_BetweenOccurrences_ReturnsNextOccurrence()
    {
        var schedule = new IntervalSchedule(Interval, Anchor);
        DateTimeOffset after = DateTimeOffset.Parse("2026-09-09T12:07:00Z");

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Equal(
            DateTimeOffset.Parse("2026-09-09T12:10:00Z"),
            result);
    }

    [Fact]
    public void GetNextOccurrence_AtOccurrence_ReturnsFollowingOccurrence()
    {
        var schedule = new IntervalSchedule(Interval, Anchor);
        DateTimeOffset after = DateTimeOffset.Parse("2026-09-09T12:10:00Z");

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Equal(
            DateTimeOffset.Parse("2026-09-09T12:15:00Z"),
            result);
    }

    [Fact]
    public void GetNextOccurrence_ManyIntervalsAfterAnchor_ReturnsNextOccurrence()
    {
        var schedule = new IntervalSchedule(Interval, Anchor);
        DateTimeOffset after = DateTimeOffset.Parse("2026-09-10T12:02:00Z");

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Equal(
            DateTimeOffset.Parse("2026-09-10T12:05:00Z"),
            result);
    }

    [Fact]
    public void Constructor_ZeroInterval_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntervalSchedule(TimeSpan.Zero, Anchor));
    }

    [Fact]
    public void Constructor_NegativeInterval_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new IntervalSchedule(TimeSpan.FromMinutes(-5), Anchor));
    }
}