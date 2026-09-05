namespace FleetOps.UnitTests.Application;

using FleetOps.Application.Abstractions.Events;
using FleetOps.Application.UseCases.Vehicles;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.ValueObjects;
using FleetOps.UnitTests.Application.Doubles;

public sealed class DomainEventsContractTests
{
    private readonly InMemoryVehicleRepository _vehicleRepository = new();
    private readonly InMemoryDomainEventDispatcher _dispatcher = new();

    private Vehicle CreateInactiveVehicle()
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("EVT-1000"),
            VehicleType.Van,
            "Ford",
            "Transit",
            2023,
            10000,
            1200m);

        vehicle.Deactivate();
        vehicle.ClearDomainEvents();
        return vehicle;
    }

    [Fact]
    public async Task DomainEvents_WhenUseCaseModifiesAggregate_ShouldRecordEventsInAggregateRoot()
    {
        var vehicle = CreateInactiveVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var unitOfWork = new InMemoryUnitOfWork();
        var useCase = new ActivateVehicleUseCase(_vehicleRepository, unitOfWork);

        await useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id));

        Assert.NotEmpty(vehicle.DomainEvents);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleActivatedDomainEvent);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitSucceeds_ShouldDispatchEventsAndClearAggregateEvents()
    {
        var vehicle = CreateInactiveVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var unitOfWork = new InMemoryUnitOfWork(_dispatcher);
        unitOfWork.TrackAggregates(() => _vehicleRepository.Entities);

        var useCase = new ActivateVehicleUseCase(_vehicleRepository, unitOfWork);
        await useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id));

        Assert.Single(_dispatcher.DispatchedEvents);
        Assert.IsType<VehicleActivatedDomainEvent>(_dispatcher.DispatchedEvents[0]);
        Assert.Empty(vehicle.DomainEvents);
    }

    [Fact]
    public async Task UnitOfWork_WhenCommitFails_ShouldNotDispatchEventsAndPreserveEventsInAggregate()
    {
        var vehicle = CreateInactiveVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var unitOfWork = new InMemoryUnitOfWork(_dispatcher);
        unitOfWork.TrackAggregates(() => _vehicleRepository.Entities);
        unitOfWork.SimulateCommitFailure();

        var useCase = new ActivateVehicleUseCase(_vehicleRepository, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id)));

        Assert.Empty(_dispatcher.DispatchedEvents);
        Assert.NotEmpty(vehicle.DomainEvents);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleActivatedDomainEvent);
    }

    [Fact]
    public async Task UnitOfWork_WhenCancellationRequested_ShouldAbortAndNotDispatchEvents()
    {
        var vehicle = CreateInactiveVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var unitOfWork = new InMemoryUnitOfWork(_dispatcher);
        unitOfWork.TrackAggregates(() => _vehicleRepository.Entities);

        var useCase = new ActivateVehicleUseCase(_vehicleRepository, unitOfWork);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id), cts.Token));

        Assert.Empty(_dispatcher.DispatchedEvents);
    }

    [Fact]
    public async Task UnitOfWork_WhenDispatchFails_ShouldPropagateExceptionAndPreserveEventsInAggregate()
    {
        var vehicle = CreateInactiveVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var failingDispatcher = new FailingDomainEventDispatcher();
        var unitOfWork = new InMemoryUnitOfWork(failingDispatcher);
        unitOfWork.TrackAggregates(() => _vehicleRepository.Entities);

        var useCase = new ActivateVehicleUseCase(_vehicleRepository, unitOfWork);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id)));

        Assert.NotEmpty(vehicle.DomainEvents);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleActivatedDomainEvent);
    }

    private sealed class FailingDomainEventDispatcher : IDomainEventDispatcher
    {
        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated domain event dispatch failure.");
        }
    }
}
