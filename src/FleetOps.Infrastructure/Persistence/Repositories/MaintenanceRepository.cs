namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using Microsoft.EntityFrameworkCore;

public sealed class MaintenanceRepository : IMaintenanceRepository
{
    private readonly FleetOpsDbContext _context;

    public MaintenanceRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Maintenance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Maintenances.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<Maintenance?> GetActiveByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        return await _context.Maintenances.FirstOrDefaultAsync(
            m => m.VehicleId == vehicleId &&
                 (m.Status == MaintenanceStatus.Scheduled || m.Status == MaintenanceStatus.InProgress),
            cancellationToken);
    }

    public async Task AddAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(maintenance);
        await _context.Maintenances.AddAsync(maintenance, cancellationToken);
    }

    public Task UpdateAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(maintenance);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = _context.Entry(maintenance);
        if (entry.State == EntityState.Detached)
        {
            _context.Maintenances.Attach(maintenance);
            entry.State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }
}
