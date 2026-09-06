namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly FleetOpsDbContext _context;

    public VehicleRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    public async Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var normalized = licensePlate.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        return await _context.Vehicles.FirstOrDefaultAsync(v => v.LicensePlate.Value == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var normalized = licensePlate.Trim().ToUpperInvariant().Replace("-", "").Replace(" ", "");
        return await _context.Vehicles.AnyAsync(v => v.LicensePlate.Value == normalized, cancellationToken);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        await _context.Vehicles.AddAsync(vehicle, cancellationToken);
    }

    public Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = _context.Entry(vehicle);
        if (entry.State == EntityState.Detached)
        {
            _context.Vehicles.Attach(vehicle);
            entry.State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }
}
