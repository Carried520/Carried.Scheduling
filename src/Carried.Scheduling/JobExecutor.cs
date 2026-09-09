namespace Carried.Scheduling;

internal sealed class JobExecutor
{
    public async Task ExecuteAsync(IJob job, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(job);

        try
        {
            await job.ExecuteAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // logging / dispatching exception to caller later
        }
    }
}