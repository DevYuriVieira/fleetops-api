namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class RouteRepository : IRouteRepository
{
    private readonly FleetOpsDbContext _context;

    public RouteRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _context.Routes.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task AddAsync(Route route, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);
        await _context.Routes.AddAsync(route, cancellationToken);
    }

    public Task UpdateAsync(Route route, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(route);

        cancellationToken.ThrowIfCancellationRequested();

        var entry = _context.Entry(route);
        if (entry.State == EntityState.Detached)
        {
            _context.Routes.Attach(route);
            entry.State = EntityState.Modified;
        }

        return Task.CompletedTask;
    }
}
