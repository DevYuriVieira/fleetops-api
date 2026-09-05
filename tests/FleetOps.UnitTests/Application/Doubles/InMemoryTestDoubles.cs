namespace FleetOps.UnitTests.Application.Doubles;

using FleetOps.Application.Abstractions.Events;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly Dictionary<Guid, Vehicle> _vehicles = new();

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _vehicles.TryGetValue(id, out var vehicle);
        return Task.FromResult(vehicle);
    }

    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var vehicle = _vehicles.Values.FirstOrDefault(v => v.LicensePlate.Value.Equals(licensePlate, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(vehicle);
    }

    public Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        var exists = _vehicles.Values.Any(v => v.LicensePlate.Value.Equals(licensePlate, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDriverRepository : IDriverRepository
{
    private readonly Dictionary<Guid, Driver> _drivers = new();

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _drivers.TryGetValue(id, out var driver);
        return Task.FromResult(driver);
    }

    public Task<Driver?> GetByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        var driver = _drivers.Values.FirstOrDefault(d => d.LicenseNumber.Equals(licenseNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(driver);
    }

    public Task<bool> ExistsByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        var exists = _drivers.Values.Any(d => d.LicenseNumber.Equals(licenseNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly Dictionary<Guid, Delivery> _deliveries = new();

    public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _deliveries.TryGetValue(id, out var delivery);
        return Task.FromResult(delivery);
    }

    public Task<Delivery?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        var delivery = _deliveries.Values.FirstOrDefault(d => d.TrackingCode.Value.Equals(trackingCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(delivery);
    }

    public Task<bool> ExistsByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        var exists = _deliveries.Values.Any(d => d.TrackingCode.Value.Equals(trackingCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        _deliveries[delivery.Id] = delivery;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        _deliveries[delivery.Id] = delivery;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRouteRepository : IRouteRepository
{
    private readonly Dictionary<Guid, Route> _routes = new();

    public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _routes.TryGetValue(id, out var route);
        return Task.FromResult(route);
    }

    public Task AddAsync(Route route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Route route, CancellationToken cancellationToken = default)
    {
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryMaintenanceRepository : IMaintenanceRepository
{
    private readonly Dictionary<Guid, Maintenance> _maintenances = new();

    public Task<Maintenance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        _maintenances.TryGetValue(id, out var maintenance);
        return Task.FromResult(maintenance);
    }

    public Task<Maintenance?> GetActiveByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        var active = _maintenances.Values.FirstOrDefault(m =>
            m.VehicleId == vehicleId &&
            (m.Status == MaintenanceStatus.Scheduled || m.Status == MaintenanceStatus.InProgress));
        return Task.FromResult(active);
    }

    public Task AddAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        _maintenances[maintenance.Id] = maintenance;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        _maintenances[maintenance.Id] = maintenance;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveChangesCallCount++;
        return Task.FromResult(1);
    }
}

public sealed class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> DispatchedEvents { get; } = new();

    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        DispatchedEvents.AddRange(events);
        return Task.CompletedTask;
    }
}
