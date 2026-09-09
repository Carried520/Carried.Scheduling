namespace Carried.Scheduling.Schedule;

public sealed class IntervalSchedule : ISchedule
{
    public TimeSpan Interval { get; }
    public DateTimeOffset Anchor { get; }
    
    public IntervalSchedule(TimeSpan interval, DateTimeOffset anchor)
    {
        if (interval <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(interval), "Interval should be greater than zero.");

        Interval = interval;
        Anchor = anchor;
    }

    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after)
    {
        if (after < Anchor)
            return Anchor + Interval;

        TimeSpan timeDiff = after - Anchor;
        long intervalCount = timeDiff.Ticks / Interval.Ticks;

        return Anchor + Interval * (intervalCount + 1);
    }
}