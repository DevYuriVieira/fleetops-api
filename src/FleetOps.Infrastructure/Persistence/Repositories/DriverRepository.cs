namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class DriverRepository : IDriverRepository
{
    private readonly FleetOpsDbContext _context;

    public DriverRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Drivers.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }

    public async Task<Driver?> GetByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        var normalized = licenseNumber.Trim().ToUpperInvariant();
        return await _context.Drivers.FirstOrDefaultAsync(d => d.LicenseNumber == normalized, cancellationToken);
    }

    public async Task<bool> ExistsByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        var normalized = licenseNumber.Trim().ToUpperInvariant();
        return await _context.Drivers.AnyAsync(d => d.LicenseNumber == normalized, cancellationToken);
    }

    public async Task AddAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(driver);
        await _context.Drivers.AddAsync(driver, cancellationToken);
    }

    public Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(driver);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = _context.Entry(driver);
        if (entry.State == EntityState.Detached)
        {
            _context.Drivers.Attach(driver);
            entry.State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }
}
