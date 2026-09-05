namespace FleetOps.Application.Abstractions.Persistence;

using FleetOps.Domain.Entities;

public interface IDeliveryRepository
{
    Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Delivery?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default);
    Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default);
    Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default);
}
