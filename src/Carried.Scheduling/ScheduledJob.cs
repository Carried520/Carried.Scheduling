using Carried.Scheduling.Schedule;

namespace Carried.Scheduling;

public sealed class ScheduledJob
{
    public required string Identity { get; init; }
    public required IJob Job { get; init; }
    public required ISchedule Schedule { get; init; }
}