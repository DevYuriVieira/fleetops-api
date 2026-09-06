namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class DeliveryRepository : IDeliveryRepository
{
    private readonly FleetOpsDbContext _context;

    public DeliveryRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Deliveries.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Delivery?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        var normalized = trackingCode.Trim().ToUpperInvariant();
        return await _context.Deliveries.FirstOrDefaultAsync(d => d.TrackingCode.Value == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        var normalized = trackingCode.Trim().ToUpperInvariant();
        return await _context.Deliveries.AnyAsync(d => d.TrackingCode.Value == normalized, cancellationToken);
    }

    public async Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);
        await _context.Deliveries.AddAsync(delivery, cancellationToken);
    }

    public Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = _context.Entry(delivery);
        if (entry.State == EntityState.Detached)
        {
            _context.Deliveries.Attach(delivery);
            entry.State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }
}
