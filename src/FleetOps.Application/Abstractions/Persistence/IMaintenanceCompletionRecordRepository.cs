namespace FleetOps.Application.Abstractions.Persistence;

public interface IMaintenanceCompletionRecordRepository
{
    Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task AddAsync(
        Guid messageId,
        Guid maintenanceId,
        Guid vehicleId,
        DateTimeOffset completedOnUtc,
        DateTimeOffset processedOnUtc,
        CancellationToken cancellationToken = default);
}
