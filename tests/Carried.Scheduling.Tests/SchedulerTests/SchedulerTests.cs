using Carried.Scheduling.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace Carried.Scheduling.Tests.SchedulerTests;

public sealed class SchedulerTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task RunAsync_DoesNotExecuteJobBeforeDueTime()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "test-job",
            Job = job,
            Schedule = new OneTimeSchedule(start.AddMinutes(5))
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(0, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_ExecutesJobWhenDueTimeIsReached()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "test-job",
            Job = job,
            Schedule = new OneTimeSchedule(start.AddMinutes(5))
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_IntervalSchedule_ExecutesAtEachOccurrence()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "recurring-job",
            Job = job,
            Schedule = new IntervalSchedule(
                TimeSpan.FromMinutes(5),
                start)
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(2, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(3, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_MissedIntervalOccurrences_ExecutesOnceAndSkipsToNextFutureOccurrence()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "recurring-job",
            Job = job,
            Schedule = new IntervalSchedule(
                TimeSpan.FromMinutes(5),
                start)
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(17));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(2, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_WhenCancelled_StopsScheduler()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "test-job",
            Job = job,
            Schedule = new OneTimeSchedule(start.AddHours(1))
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));

        Assert.Equal(0, job.ExecutionCount);
    }

    [Fact]
    public async Task RunAsync_JobThrows_ContinuesScheduling()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new FailingOnceJob();

        var scheduledJob = new ScheduledJob
        {
            Identity = "failing-job",
            Job = job,
            Schedule = new IntervalSchedule(
                TimeSpan.FromMinutes(5),
                start)
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(2, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_JobThrowsSchedulerCancellation_PropagatesCancellation()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);

        using var cts = new CancellationTokenSource();

        var job = new CancellingJob(cts);

        var scheduledJob = new ScheduledJob
        {
            Identity = "cancellation-job",
            Job = job,
            Schedule = new OneTimeSchedule(start.AddMinutes(5))
        };

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(scheduledJob);

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_ExecutesJobsConcurrentlyUpToConfiguredLimit()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);

        var firstJob = new BlockingJob();
        var secondJob = new BlockingJob();

        var scheduler = new Scheduler(
            timeProvider,
            maxConcurrency: 2);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "first",
                Job = firstJob,
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "second",
                Job = secondJob,
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await Task.WhenAll(
                firstJob.Started,
                secondJob.Started)
            .WaitAsync(TestTimeout);

        firstJob.Complete();
        secondJob.Complete();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RunAsync_DoesNotExceedConfiguredConcurrency()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var tracker = new ConcurrencyTracker();

        var scheduler = new Scheduler(
            timeProvider,
            maxConcurrency: 2);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "first",
                Job = new TrackedBlockingJob(tracker),
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "second",
                Job = new TrackedBlockingJob(tracker),
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "third",
                Job = new TrackedBlockingJob(tracker),
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await tracker.TwoJobsStarted.WaitAsync(TestTimeout);

        Assert.Equal(2, tracker.StartedCount);
        Assert.Equal(2, tracker.MaxConcurrency);

        tracker.ReleaseOne();

        await tracker.ThreeJobsStarted.WaitAsync(TestTimeout);

        Assert.Equal(3, tracker.StartedCount);
        Assert.Equal(2, tracker.MaxConcurrency);

        tracker.ReleaseAll();

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RegisterAsync_WhileSchedulerIsRunning_SchedulesJob()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);
        var job = new TestJob();

        var scheduler = new Scheduler(timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "dynamic-job",
                Job = job,
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await job.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RegisterAsync_EarlierJob_WakesSchedulerWaitingForLaterOccurrence()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);

        var laterJob = new TestJob();
        var earlierJob = new TestJob();

        var scheduler = new Scheduler(timeProvider);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "later-job",
                Job = laterJob,
                Schedule = new OneTimeSchedule(start.AddHours(1))
            });

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "earlier-job",
                Job = earlierJob,
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await earlierJob.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, earlierJob.ExecutionCount);
        Assert.Equal(0, laterJob.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public async Task RegisterAsync_MultipleJobsWhileRunning_SchedulesAllJobs()
    {
        DateTimeOffset start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
        var timeProvider = new FakeTimeProvider(start);

        var firstJob = new TestJob();
        var secondJob = new TestJob();

        var scheduler = new Scheduler(timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "first",
                Job = firstJob,
                Schedule = new OneTimeSchedule(start.AddMinutes(5))
            });

        await scheduler.RegisterAsync(
            new ScheduledJob
            {
                Identity = "second",
                Job = secondJob,
                Schedule = new OneTimeSchedule(start.AddMinutes(10))
            });

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await firstJob.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, firstJob.ExecutionCount);
        Assert.Equal(0, secondJob.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await secondJob.WaitForExecutionAsync().WaitAsync(TestTimeout);

        Assert.Equal(1, secondJob.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask.WaitAsync(TestTimeout));
    }

    [Fact]
    public void RegisterAsync_NullJob_ThrowsArgumentNullException()
    {
        var scheduler = new Scheduler();

        Assert.Throws<ArgumentNullException>(() =>
        {
            scheduler.RegisterAsync(null!);
        });
    }

    private sealed class BlockingJob : IJob
    {
        private readonly TaskCompletionSource<bool> _started =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Started => _started.Task;

        public void Complete()
        {
            _completion.TrySetResult(true);
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken)
        {
            _started.TrySetResult(true);

            await _completion.Task.WaitAsync(cancellationToken);
        }
    }

    private sealed class CancellingJob(
        CancellationTokenSource cancellationTokenSource) : IJob
    {
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationTokenSource.Cancel();
            cancellationToken.ThrowIfCancellationRequested();

            return Task.CompletedTask;
        }
    }

    private sealed class FailingOnceJob : IJob
    {
        private readonly SemaphoreSlim _executionSignal = new(0);
        private int _executionCount;

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public Task WaitForExecutionAsync()
        {
            return _executionSignal.WaitAsync();
        }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            int executionCount =
                Interlocked.Increment(ref _executionCount);

            _executionSignal.Release();

            if (executionCount == 1)
                throw new InvalidOperationException("Job failed.");

            return Task.CompletedTask;
        }
    }

    private sealed class TestJob : IJob
    {
        private readonly SemaphoreSlim _executionSignal = new(0);
        private int _executionCount;

        public int ExecutionCount => Volatile.Read(ref _executionCount);

        public Task WaitForExecutionAsync()
        {
            return _executionSignal.WaitAsync();
        }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _executionCount);
            _executionSignal.Release();

            return Task.CompletedTask;
        }
    }

    private sealed class ConcurrencyTracker
    {
        private readonly SemaphoreSlim _gate = new(0);

        private readonly TaskCompletionSource<bool> _twoJobsStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly TaskCompletionSource<bool> _threeJobsStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        private int _startedCount;
        private int _currentConcurrency;
        private int _maxConcurrency;

        public int StartedCount => Volatile.Read(ref _startedCount);

        public int MaxConcurrency => Volatile.Read(ref _maxConcurrency);

        public Task TwoJobsStarted => _twoJobsStarted.Task;

        public Task ThreeJobsStarted => _threeJobsStarted.Task;

        public async Task EnterAsync(
            CancellationToken cancellationToken)
        {
            int startedCount =
                Interlocked.Increment(ref _startedCount);

            int currentConcurrency =
                Interlocked.Increment(ref _currentConcurrency);

            UpdateMaxConcurrency(currentConcurrency);

            if (startedCount == 2)
                _twoJobsStarted.TrySetResult(true);

            if (startedCount == 3)
                _threeJobsStarted.TrySetResult(true);

            try
            {
                await _gate.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _currentConcurrency);
            }
        }

        public void ReleaseOne()
        {
            _gate.Release();
        }

        public void ReleaseAll()
        {
            _gate.Release(2);
        }

        private void UpdateMaxConcurrency(int concurrency)
        {
            while (true)
            {
                int currentMax =
                    Volatile.Read(ref _maxConcurrency);

                if (concurrency <= currentMax)
                    return;

                if (Interlocked.CompareExchange(
                        ref _maxConcurrency,
                        concurrency,
                        currentMax) == currentMax)
                    return;
            }
        }
    }

    private sealed class TrackedBlockingJob(
        ConcurrencyTracker tracker) : IJob
    {
        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            return tracker.EnterAsync(cancellationToken);
        }
    }
}