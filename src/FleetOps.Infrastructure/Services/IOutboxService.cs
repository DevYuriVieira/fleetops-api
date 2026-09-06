namespace FleetOps.Infrastructure.Services;

public interface IOutboxService
{
    Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default);
}
