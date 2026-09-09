using System.Threading.Channels;

namespace Carried.Scheduling;

public sealed class Scheduler
{
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<ScheduledJob> _registeredJobs;
    private readonly JobExecutor _jobExecutor = new();
    private readonly PriorityQueue<ScheduledJob, DateTimeOffset> _scheduleQueue = new();

    private readonly Channel<ScheduledJob> _executionChannel =
        Channel.CreateUnbounded<ScheduledJob>();

    public Scheduler(IEnumerable<ScheduledJob> registeredJobs, TimeProvider? timeProvider = null)
    {
        _registeredJobs = registeredJobs.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await Task.WhenAll(RunSchedulingLoopAsync(cancellationToken), RunExecutionLoopAsync(cancellationToken));
    }

    private async Task RunSchedulingLoopAsync(CancellationToken cancellationToken = default)
    {
        try
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

                await _executionChannel.Writer.WriteAsync(scheduledJob, cancellationToken);

                now = _timeProvider.GetUtcNow();
                DateTimeOffset? nextJobOccurrence = scheduledJob.Schedule.GetNextOccurrence(now);

                if (nextJobOccurrence.HasValue)
                    _scheduleQueue.Enqueue(scheduledJob, nextJobOccurrence.Value);
            }
        }
        finally
        {
            _executionChannel.Writer.TryComplete();
        }
    }

    private async Task RunExecutionLoopAsync(CancellationToken cancellationToken = default)
    {
        await foreach (ScheduledJob job in _executionChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await _jobExecutor.ExecuteAsync(job.Job, cancellationToken);
        }
    }
}