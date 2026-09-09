using System.Threading.Channels;

namespace Carried.Scheduling;

public sealed class Scheduler
{
    private readonly TimeProvider _timeProvider;
    private readonly JobExecutor _jobExecutor = new();
    private readonly PriorityQueue<ScheduledJob, DateTimeOffset> _scheduleQueue = new();
    private readonly int _maxConcurrency;

    private readonly Channel<JobRegistration> _registrationChannel =
        Channel.CreateUnbounded<JobRegistration>();

    private readonly Channel<ScheduledJob> _executionChannel =
        Channel.CreateUnbounded<ScheduledJob>();

    public Scheduler(
        TimeProvider? timeProvider = null,
        int maxConcurrency = 1)
    {
        if (maxConcurrency <= 0)
            throw new ArgumentOutOfRangeException(
                nameof(maxConcurrency),
                "Maximum concurrency must be greater than zero.");

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

    public ValueTask RegisterAsync(ScheduledJob scheduledJob, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scheduledJob);

        var registration = new JobRegistration(scheduledJob, _timeProvider.GetUtcNow());

        return _registrationChannel.Writer.WriteAsync(registration, cancellationToken);
    }

    private async Task RunSchedulingLoopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();

                while (_registrationChannel.Reader.TryRead(out JobRegistration? registration))
                {
                    RegisterJob(registration);
                }

                if (!_scheduleQueue.TryPeek(
                        out ScheduledJob? nextJob,
                        out DateTimeOffset dueAt))
                {
                    JobRegistration registration = await _registrationChannel.Reader.ReadAsync(cancellationToken);

                    RegisterJob(registration);
                    continue;
                }

                DateTimeOffset now = _timeProvider.GetUtcNow();

                if (now >= dueAt)
                {
                    _scheduleQueue.Dequeue();

                    await _executionChannel.Writer.WriteAsync(nextJob, cancellationToken);

                    now = _timeProvider.GetUtcNow();

                    DateTimeOffset? nextOccurrence = nextJob.Schedule.GetNextOccurrence(now);

                    if (nextOccurrence.HasValue)
                    {
                        _scheduleQueue.Enqueue(nextJob, nextOccurrence.Value);
                    }

                    continue;
                }

                using var waitCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                Task delayTask = Task.Delay(dueAt - now, _timeProvider, waitCts.Token);

                Task<bool> registrationTask = _registrationChannel.Reader.WaitToReadAsync(waitCts.Token).AsTask();

                Task completedTask = await Task.WhenAny(delayTask, registrationTask);

                await completedTask;
                await waitCts.CancelAsync();
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

    private void RegisterJob(JobRegistration registration)
    {
        DateTimeOffset? nextOccurrence = registration.Job.Schedule.GetNextOccurrence(registration.RegisteredAt);

        if (nextOccurrence is not null)
        {
            _scheduleQueue.Enqueue(registration.Job , nextOccurrence.Value);
        }
    }

    private sealed record JobRegistration(
        ScheduledJob Job,
        DateTimeOffset RegisteredAt);
}