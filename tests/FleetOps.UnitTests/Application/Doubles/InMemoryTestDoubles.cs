namespace FleetOps.UnitTests.Application.Doubles;

using FleetOps.Application.Abstractions.Events;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Primitives;

public sealed class InMemoryVehicleRepository : IVehicleRepository
{
    private readonly Dictionary<Guid, Vehicle> _vehicles = new();
    private readonly HashSet<Guid> _updatedIds = new();
    private readonly HashSet<Guid> _addedIds = new();

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public IEnumerable<Vehicle> Entities => _vehicles.Values;

    public bool WasUpdated(Guid id) => _updatedIds.Contains(id);
    public bool WasAdded(Guid id) => _addedIds.Contains(id);

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _vehicles.TryGetValue(id, out var vehicle);
        return Task.FromResult(vehicle);
    }

    public Task<Vehicle?> GetByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var vehicle = _vehicles.Values.FirstOrDefault(v => v.LicensePlate.Value.Equals(licensePlate, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(vehicle);
    }

    public Task<bool> ExistsByLicensePlateAsync(string licensePlate, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = _vehicles.Values.Any(v => v.LicensePlate.Value.Equals(licensePlate, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCallCount++;
        _addedIds.Add(vehicle.Id);
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCallCount++;
        _updatedIds.Add(vehicle.Id);
        _vehicles[vehicle.Id] = vehicle;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDriverRepository : IDriverRepository
{
    private readonly Dictionary<Guid, Driver> _drivers = new();
    private readonly HashSet<Guid> _updatedIds = new();
    private readonly HashSet<Guid> _addedIds = new();

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public IEnumerable<Driver> Entities => _drivers.Values;

    public bool WasUpdated(Guid id) => _updatedIds.Contains(id);
    public bool WasAdded(Guid id) => _addedIds.Contains(id);

    public Task<Driver?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _drivers.TryGetValue(id, out var driver);
        return Task.FromResult(driver);
    }

    public Task<Driver?> GetByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var driver = _drivers.Values.FirstOrDefault(d => d.LicenseNumber.Equals(licenseNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(driver);
    }

    public Task<bool> ExistsByLicenseNumberAsync(string licenseNumber, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = _drivers.Values.Any(d => d.LicenseNumber.Equals(licenseNumber, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCallCount++;
        _addedIds.Add(driver.Id);
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Driver driver, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCallCount++;
        _updatedIds.Add(driver.Id);
        _drivers[driver.Id] = driver;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryDeliveryRepository : IDeliveryRepository
{
    private readonly Dictionary<Guid, Delivery> _deliveries = new();
    private readonly HashSet<Guid> _updatedIds = new();
    private readonly HashSet<Guid> _addedIds = new();

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public IEnumerable<Delivery> Entities => _deliveries.Values;

    public bool WasUpdated(Guid id) => _updatedIds.Contains(id);
    public bool WasAdded(Guid id) => _addedIds.Contains(id);

    public Task<Delivery?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _deliveries.TryGetValue(id, out var delivery);
        return Task.FromResult(delivery);
    }

    public Task<Delivery?> GetByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var delivery = _deliveries.Values.FirstOrDefault(d => d.TrackingCode.Value.Equals(trackingCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(delivery);
    }

    public Task<bool> ExistsByTrackingCodeAsync(string trackingCode, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exists = _deliveries.Values.Any(d => d.TrackingCode.Value.Equals(trackingCode, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(exists);
    }

    public Task AddAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCallCount++;
        _addedIds.Add(delivery.Id);
        _deliveries[delivery.Id] = delivery;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Delivery delivery, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCallCount++;
        _updatedIds.Add(delivery.Id);
        _deliveries[delivery.Id] = delivery;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryRouteRepository : IRouteRepository
{
    private readonly Dictionary<Guid, Route> _routes = new();
    private readonly HashSet<Guid> _updatedIds = new();
    private readonly HashSet<Guid> _addedIds = new();

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public IEnumerable<Route> Entities => _routes.Values;

    public bool WasUpdated(Guid id) => _updatedIds.Contains(id);
    public bool WasAdded(Guid id) => _addedIds.Contains(id);

    public Task<Route?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _routes.TryGetValue(id, out var route);
        return Task.FromResult(route);
    }

    public Task AddAsync(Route route, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCallCount++;
        _addedIds.Add(route.Id);
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Route route, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCallCount++;
        _updatedIds.Add(route.Id);
        _routes[route.Id] = route;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryMaintenanceRepository : IMaintenanceRepository
{
    private readonly Dictionary<Guid, Maintenance> _maintenances = new();
    private readonly HashSet<Guid> _updatedIds = new();
    private readonly HashSet<Guid> _addedIds = new();

    public int AddCallCount { get; private set; }
    public int UpdateCallCount { get; private set; }
    public IEnumerable<Maintenance> Entities => _maintenances.Values;

    public bool WasUpdated(Guid id) => _updatedIds.Contains(id);
    public bool WasAdded(Guid id) => _addedIds.Contains(id);

    public Task<Maintenance?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _maintenances.TryGetValue(id, out var maintenance);
        return Task.FromResult(maintenance);
    }

    public Task<Maintenance?> GetActiveByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var active = _maintenances.Values.FirstOrDefault(m =>
            m.VehicleId == vehicleId &&
            (m.Status == MaintenanceStatus.Scheduled || m.Status == MaintenanceStatus.InProgress));
        return Task.FromResult(active);
    }

    public Task AddAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        AddCallCount++;
        _addedIds.Add(maintenance.Id);
        _maintenances[maintenance.Id] = maintenance;
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Maintenance maintenance, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        UpdateCallCount++;
        _updatedIds.Add(maintenance.Id);
        _maintenances[maintenance.Id] = maintenance;
        return Task.CompletedTask;
    }
}

public sealed class InMemoryUnitOfWork : IUnitOfWork
{
    private readonly IDomainEventDispatcher? _dispatcher;
    private readonly List<Func<IEnumerable<AggregateRoot>>> _aggregateTrackers = new();
    private bool _shouldFailOnCommit;

    public int SaveChangesCallCount { get; private set; }

    public InMemoryUnitOfWork(IDomainEventDispatcher? dispatcher = null)
    {
        _dispatcher = dispatcher;
    }

    public void TrackAggregates(Func<IEnumerable<AggregateRoot>> tracker)
    {
        _aggregateTrackers.Add(tracker);
    }

    public void SimulateCommitFailure()
    {
        _shouldFailOnCommit = true;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_shouldFailOnCommit)
        {
            throw new InvalidOperationException("Simulated database commit failure.");
        }

        SaveChangesCallCount++;

        if (_dispatcher is not null && _aggregateTrackers.Count > 0)
        {
            var aggregates = _aggregateTrackers.SelectMany(t => t()).ToList();
            var events = aggregates.SelectMany(a => a.DomainEvents).ToList();

            if (events.Count > 0)
            {
                await _dispatcher.DispatchEventsAsync(events, cancellationToken);
                foreach (var aggregate in aggregates)
                {
                    aggregate.ClearDomainEvents();
                }
            }
        }

        return 1;
    }
}

public sealed class InMemoryDomainEventDispatcher : IDomainEventDispatcher
{
    public List<IDomainEvent> DispatchedEvents { get; } = new();

    public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DispatchedEvents.AddRange(events);
        return Task.CompletedTask;
    }
}
