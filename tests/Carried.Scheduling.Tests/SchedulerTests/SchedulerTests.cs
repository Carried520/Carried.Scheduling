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
        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(2, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(3, job.ExecutionCount);

        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await runTask);
    }
    
    
    [Fact]
    public async Task RunAsync_MissedIntervalOccurrences_ExecutesOnceAndSkipsToNextFutureOccurrence()
    {
        var start = DateTimeOffset.Parse("2026-09-09T12:00:00Z");
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

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(2));

        Assert.Equal(1, job.ExecutionCount);

        timeProvider.Advance(TimeSpan.FromMinutes(1));

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

    private sealed class TestJob : IJob
    {
        public int ExecutionCount { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken)
        {
            ExecutionCount++;
            return Task.CompletedTask;
        }
    }
}