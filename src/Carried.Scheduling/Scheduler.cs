using System.Threading.Channels;

namespace Carried.Scheduling;

public sealed class Scheduler
{
    private readonly TimeProvider _timeProvider;
    private readonly IReadOnlyList<ScheduledJob> _registeredJobs;
    private readonly JobExecutor _jobExecutor = new();
    private readonly PriorityQueue<ScheduledJob, DateTimeOffset> _scheduleQueue = new();
    private readonly int _maxConcurrency;

    private readonly Channel<ScheduledJob> _executionChannel =
        Channel.CreateUnbounded<ScheduledJob>();

    public Scheduler(IEnumerable<ScheduledJob> registeredJobs,
        TimeProvider? timeProvider = null,
        int maxConcurrency = 1)
    {
        ArgumentNullException.ThrowIfNull(registeredJobs);

        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                "Maximum concurrency must be greater than zero.");

        _registeredJobs = registeredJobs.ToArray();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _maxConcurrency = maxConcurrency;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        Task schedulingTask = RunSchedulingLoopAsync(cancellationToken);

        Task[] executionTasks = Enumerable.Range(0, _maxConcurrency)
            .Select(_ => RunExecutionLoopAsync(cancellationToken))
            .ToArray();

        await Task.WhenAll(executionTasks.Prepend(schedulingTask));
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
        await foreach (ScheduledJob scheduledJob in _executionChannel.Reader.ReadAllAsync(cancellationToken))
        {
            await _jobExecutor.ExecuteAsync(scheduledJob.Job, cancellationToken);
        }
    }
}