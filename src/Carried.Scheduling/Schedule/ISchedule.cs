namespace Carried.Scheduling.Schedule;

public interface ISchedule
{
    DateTimeOffset? GetNextOccurrence(DateTimeOffset after);
}