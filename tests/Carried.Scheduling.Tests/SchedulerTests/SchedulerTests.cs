using Carried.Scheduling.Schedule;
using Microsoft.Extensions.Time.Testing;

namespace Carried.Scheduling.Tests.SchedulerTests;

public sealed class SchedulerTests
{
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(4));

        Assert.Equal(0, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        Task runTask = scheduler.RunAsync();

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await runTask;

        Assert.Equal(1, job.ExecutionCount);
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync();

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync();

        Assert.Equal(2, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync();

        Assert.Equal(3, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(17));
        await job.WaitForExecutionAsync();

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(1));
        await job.WaitForExecutionAsync();

        Assert.Equal(2, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);

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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        using var cts = new CancellationTokenSource();

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync();

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        await job.WaitForExecutionAsync();

        Assert.Equal(2, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
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

        var scheduler = new Scheduler(
            [scheduledJob],
            timeProvider);

        Task runTask = scheduler.RunAsync(cts.Token);

        timeProvider.Advance(TimeSpan.FromMinutes(5));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
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

        public int ExecutionCount { get; private set; }

        public Task WaitForExecutionAsync()
        {
            return _executionSignal.WaitAsync();
        }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            _executionSignal.Release();

            if (ExecutionCount == 1)
                throw new InvalidOperationException("Job failed.");

            return Task.CompletedTask;
        }
    }

    private sealed class TestJob : IJob
    {
        private readonly SemaphoreSlim _executionSignal = new(0);

        public int ExecutionCount { get; private set; }

        public Task WaitForExecutionAsync()
        {
            return _executionSignal.WaitAsync();
        }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            _executionSignal.Release();

            return Task.CompletedTask;
        }
    }
}