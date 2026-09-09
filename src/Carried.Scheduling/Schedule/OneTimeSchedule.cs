namespace Carried.Scheduling.Schedule;

public sealed record OneTimeSchedule(DateTimeOffset ExecutionTime) : ISchedule
{
    public DateTimeOffset? GetNextOccurrence(DateTimeOffset after) => ExecutionTime > after ? ExecutionTime : null;
}