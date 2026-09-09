namespace Carried.Scheduling;

public sealed class Scheduler
{
    private readonly TimeProvider _timeProvider;
    private readonly IEnumerable<ScheduledJob> _registeredJobs;
    private readonly PriorityQueue<ScheduledJob, DateTimeOffset> _scheduleQueue = new();

    public Scheduler(IEnumerable<ScheduledJob> registeredJobs, TimeProvider? timeProvider = null)
    {
        _registeredJobs = registeredJobs;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset now = _timeProvider.GetUtcNow();

        foreach (ScheduledJob scheduledJob in _registeredJobs)
        {
            DateTimeOffset? nextOccurrence = scheduledJob.Schedule.GetNextOccurrence(now);

            if (nextOccurrence is not null)
            {
                _scheduleQueue.Enqueue(scheduledJob, nextOccurrence.Value);
            }
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!_scheduleQueue.TryPeek(
                    out ScheduledJob? scheduledJob,
                    out DateTimeOffset dueAt))
                break;

            now = _timeProvider.GetUtcNow();

            if (now < dueAt)
                await Task.Delay(dueAt - now, _timeProvider, cancellationToken);

            _scheduleQueue.Dequeue();
            await scheduledJob.Job.ExecuteAsync(cancellationToken);

            now = _timeProvider.GetUtcNow();
            DateTimeOffset? nextJobOccurence = scheduledJob.Schedule.GetNextOccurrence(now);

            if (nextJobOccurence.HasValue)
                _scheduleQueue.Enqueue(scheduledJob, nextJobOccurence.Value);
        }
    }
}