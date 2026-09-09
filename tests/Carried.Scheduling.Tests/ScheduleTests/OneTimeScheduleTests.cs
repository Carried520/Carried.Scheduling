using Carried.Scheduling.Schedule;

namespace Carried.Scheduling.Tests.ScheduleTests;

public class OneTimeScheduleTests
{
    [Fact]
    public void GetNextOccurrence_BeforeExecutionTime_ReturnsExecutionTime()
    {
        DateTimeOffset executionTime = DateTimeOffset.Parse("2026-09-09T14:00:00Z");
        DateTimeOffset after = DateTimeOffset.Parse("2026-09-09T13:00:00Z");

        var schedule = new OneTimeSchedule(executionTime);

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Equal(executionTime, result);
    }

    [Fact]
    public void GetNextOccurrence_AtExecutionTime_ReturnsNull()
    {
        DateTimeOffset executionTime = DateTimeOffset.Parse("2026-09-09T14:00:00Z");
        var schedule = new OneTimeSchedule(executionTime);

        DateTimeOffset? result = schedule.GetNextOccurrence(executionTime);

        Assert.Null(result);
    }

    [Fact]
    public void GetNextOccurrence_AfterExecutionTime_ReturnsNull()
    {
        DateTimeOffset executionTime = DateTimeOffset.Parse("2026-09-09T14:00:00Z");
        DateTimeOffset after = DateTimeOffset.Parse("2026-09-09T15:00:00Z");
        var schedule = new OneTimeSchedule(executionTime);

        DateTimeOffset? result = schedule.GetNextOccurrence(after);

        Assert.Null(result);
    }
}