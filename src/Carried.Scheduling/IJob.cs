namespace Carried.Scheduling;

public interface IJob
{
    Task ExecuteAsync(CancellationToken cancellationToken);
}