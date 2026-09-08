namespace Carried.Scheduling;

public interface ISchedule
{
    DateTimeOffset? GetNextOccurrence(DateTimeOffset after);
}