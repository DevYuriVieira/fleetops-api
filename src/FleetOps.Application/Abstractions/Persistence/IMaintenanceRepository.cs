namespace FleetOps.Application.Abstractions.Persistence;

using FleetOps.Domain.Entities;

public interface IMaintenanceRepository
{
    Task<Maintenance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Maintenance?> GetActiveByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task AddAsync(Maintenance maintenance, CancellationToken cancellationToken = default);
    Task UpdateAsync(Maintenance maintenance, CancellationToken cancellationToken = default);
}
