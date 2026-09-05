namespace FleetOps.UnitTests.Application;

using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Application.UseCases.Routes;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;
using FleetOps.UnitTests.Application.Doubles;

public sealed class RouteUseCasesTests
{
    private readonly InMemoryRouteRepository _routeRepository = new();
    private readonly InMemoryDeliveryRepository _deliveryRepository = new();
    private readonly InMemoryVehicleRepository _vehicleRepository = new();
    private readonly InMemoryDriverRepository _driverRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();

    private static AddressDto CreateSampleAddressDto(string street = "Av. Paulista", string number = "1000") =>
        new(street, number, "Bela Vista", "São Paulo", "SP", "01310-100", "Brasil");

    private static Address CreateSampleAddress(string street = "Av. Paulista", string number = "1000") =>
        new(street, number, "Bela Vista", "São Paulo", "SP", "01310-100", "Brasil");

    private Route CreateTestRoute(RouteStatus status = RouteStatus.Planned, bool withAssignmentAndDeliveries = false)
    {
        var origin = CreateSampleAddress("Rua A", "10");
        var destination = CreateSampleAddress("Rua B", "20");
        var plannedDeparture = DateTimeOffset.UtcNow.AddHours(1);
        var estimatedArrival = DateTimeOffset.UtcNow.AddHours(5);

        var route = Route.Create(
            Guid.NewGuid(),
            origin,
            destination,
            plannedDeparture,
            estimatedArrival);

        if (withAssignmentAndDeliveries)
        {
            route.Assign(Guid.NewGuid(), Guid.NewGuid());
            route.AddDelivery(Guid.NewGuid());
        }

        if (status == RouteStatus.InProgress || status == RouteStatus.Completed)
        {
            if (!withAssignmentAndDeliveries)
            {
                route.Assign(Guid.NewGuid(), Guid.NewGuid());
                route.AddDelivery(Guid.NewGuid());
            }
            route.Start(DateTimeOffset.UtcNow);
        }

        if (status == RouteStatus.Completed)
        {
            route.Complete(DateTimeOffset.UtcNow.AddHours(4));
        }
        else if (status == RouteStatus.Cancelled)
        {
            route.Cancel("Cancelled for test");
        }

        return route;
    }

    private Vehicle CreateTestVehicle(VehicleStatus status = VehicleStatus.Active)
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC-1234"),
            VehicleType.Truck,
            "Scania",
            "R500",
            2023,
            20000,
            25000m);

        if (status == VehicleStatus.Inactive)
        {
            vehicle.Deactivate();
        }

        return vehicle;
    }

    private Driver CreateTestDriver(DriverStatus status = DriverStatus.Active)
    {
        var driver = Driver.Create(
            Guid.NewGuid(),
            "Fernando Torres",
            "CNH77889900",
            "fernando@fleetops.com",
            "+5511944443333");

        if (status == DriverStatus.Suspended)
        {
            driver.Suspend("License renewal");
        }

        return driver;
    }

    private Delivery CreateTestDelivery(DeliveryStatus status = DeliveryStatus.Pending)
    {
        var delivery = Delivery.Create(
            Guid.NewGuid(),
            TrackingCode.Create("BR123456789XP"),
            CreateSampleAddress("Rua X", "1"),
            CreateSampleAddress("Rua Y", "2"),
            DeliveryPriority.Standard,
            50m);

        if (status is DeliveryStatus.Assigned or DeliveryStatus.InTransit or DeliveryStatus.Delivered)
        {
            delivery.Assign(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(2));
        }

        if (status is DeliveryStatus.InTransit or DeliveryStatus.Delivered)
        {
            delivery.Start();
        }

        if (status == DeliveryStatus.Delivered)
        {
            delivery.Complete(DateTimeOffset.UtcNow.AddHours(1));
        }
        else if (status == DeliveryStatus.Cancelled)
        {
            delivery.Cancel("Cancelled for test");
        }

        return delivery;
    }

    [Fact]
    public async Task CreateRoute_WithValidCommand_ShouldCreateAndPersistRoute()
    {
        var useCase = new CreateRouteUseCase(_routeRepository, _unitOfWork);
        var planned = DateTimeOffset.UtcNow.AddHours(2);
        var arrival = DateTimeOffset.UtcNow.AddHours(6);
        var command = new CreateRouteCommand(
            CreateSampleAddressDto("Origem", "1"),
            CreateSampleAddressDto("Destino", "2"),
            planned,
            arrival);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(RouteStatus.Planned.ToString(), result.Status);
        Assert.Equal(planned, result.PlannedDeparture);
        Assert.Equal(arrival, result.EstimatedArrival);
        Assert.Empty(result.DeliveryIds);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var stored = await _routeRepository.GetByIdAsync(result.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task CreateRoute_WhenCancellationRequested_ShouldThrowOperationCanceledException()
    {
        var useCase = new CreateRouteUseCase(_routeRepository, _unitOfWork);
        var command = new CreateRouteCommand(
            CreateSampleAddressDto("Origem", "1"),
            CreateSampleAddressDto("Destino", "2"),
            DateTimeOffset.UtcNow.AddHours(2),
            DateTimeOffset.UtcNow.AddHours(6));

        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => useCase.ExecuteAsync(command, cts.Token));
    }

    [Fact]
    public async Task CreateRoute_WhenArrivalBeforeDeparture_ShouldThrowDomainValidationException()
    {
        var useCase = new CreateRouteUseCase(_routeRepository, _unitOfWork);
        var planned = DateTimeOffset.UtcNow.AddHours(6);
        var arrival = DateTimeOffset.UtcNow.AddHours(2);
        var command = new CreateRouteCommand(
            CreateSampleAddressDto("Origem", "1"),
            CreateSampleAddressDto("Destino", "2"),
            planned,
            arrival);

        await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AssignRoute_WithValidActiveVehicleAndDriver_ShouldAssignSuccessfully()
    {
        var route = CreateTestRoute();
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver();

        await _routeRepository.AddAsync(route);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignRouteUseCase(_routeRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignRouteCommand(route.Id, vehicle.Id, driver.Id);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(vehicle.Id, result.AssignedVehicleId);
        Assert.Equal(driver.Id, result.AssignedDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task AssignRoute_WhenVehicleInactive_ShouldThrowConflictException()
    {
        var route = CreateTestRoute();
        var vehicle = CreateTestVehicle(VehicleStatus.Inactive);
        var driver = CreateTestDriver();

        await _routeRepository.AddAsync(route);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignRouteUseCase(_routeRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignRouteCommand(route.Id, vehicle.Id, driver.Id);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AssignRoute_WhenDriverSuspended_ShouldThrowConflictException()
    {
        var route = CreateTestRoute();
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver(DriverStatus.Suspended);

        await _routeRepository.AddAsync(route);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignRouteUseCase(_routeRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignRouteCommand(route.Id, vehicle.Id, driver.Id);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AddDeliveryToRoute_WhenRouteAndDeliveryExist_ShouldAddDelivery()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery();

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new AddDeliveryToRouteUseCase(_routeRepository, _deliveryRepository, _unitOfWork);
        var command = new AddDeliveryToRouteCommand(route.Id, delivery.Id);

        var result = await useCase.ExecuteAsync(command);

        Assert.Contains(delivery.Id, result.DeliveryIds);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task AddDeliveryToRoute_WhenDeliveryIsCancelled_ShouldThrowConflictException()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery(DeliveryStatus.Cancelled);

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new AddDeliveryToRouteUseCase(_routeRepository, _deliveryRepository, _unitOfWork);
        var command = new AddDeliveryToRouteCommand(route.Id, delivery.Id);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AddDeliveryToRoute_WhenDeliveryIsDelivered_ShouldThrowConflictException()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery(DeliveryStatus.Delivered);

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new AddDeliveryToRouteUseCase(_routeRepository, _deliveryRepository, _unitOfWork);
        var command = new AddDeliveryToRouteCommand(route.Id, delivery.Id);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AddDeliveryToRoute_WhenDeliveryIsInTransit_ShouldThrowConflictException()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery(DeliveryStatus.InTransit);

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new AddDeliveryToRouteUseCase(_routeRepository, _deliveryRepository, _unitOfWork);
        var command = new AddDeliveryToRouteCommand(route.Id, delivery.Id);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AddDeliveryToRoute_WhenDeliveryIsAssigned_ShouldAddSuccessfully()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery(DeliveryStatus.Assigned);

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new AddDeliveryToRouteUseCase(_routeRepository, _deliveryRepository, _unitOfWork);
        var command = new AddDeliveryToRouteCommand(route.Id, delivery.Id);

        var result = await useCase.ExecuteAsync(command);

        Assert.Contains(delivery.Id, result.DeliveryIds);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task RemoveDeliveryFromRoute_WhenDeliveryInRoute_ShouldRemove()
    {
        var route = CreateTestRoute();
        var delivery = CreateTestDelivery();
        route.AddDelivery(delivery.Id);

        await _routeRepository.AddAsync(route);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new RemoveDeliveryFromRouteUseCase(_routeRepository, _unitOfWork);
        var command = new RemoveDeliveryFromRouteCommand(route.Id, delivery.Id);

        var result = await useCase.ExecuteAsync(command);

        Assert.DoesNotContain(delivery.Id, result.DeliveryIds);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task RemoveDeliveryFromRoute_WhenDeliveryNotInRoute_ShouldThrowInvalidRouteStateException()
    {
        var route = CreateTestRoute();
        await _routeRepository.AddAsync(route);

        var useCase = new RemoveDeliveryFromRouteUseCase(_routeRepository, _unitOfWork);
        var command = new RemoveDeliveryFromRouteCommand(route.Id, Guid.NewGuid());

        await Assert.ThrowsAsync<InvalidRouteStateException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task StartRoute_WhenAssignedAndHasDeliveries_ShouldStartRoute()
    {
        var route = CreateTestRoute(withAssignmentAndDeliveries: true);
        await _routeRepository.AddAsync(route);

        var useCase = new StartRouteUseCase(_routeRepository, _unitOfWork);
        var departure = DateTimeOffset.UtcNow;
        var result = await useCase.ExecuteAsync(new StartRouteCommand(route.Id, departure));

        Assert.Equal(RouteStatus.InProgress.ToString(), result.Status);
        Assert.Equal(departure, result.ActualDeparture);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task StartRoute_WithoutDeliveries_ShouldThrowInvalidRouteStateException()
    {
        var route = CreateTestRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        await _routeRepository.AddAsync(route);

        var useCase = new StartRouteUseCase(_routeRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidRouteStateException>(() =>
            useCase.ExecuteAsync(new StartRouteCommand(route.Id)));
    }

    [Fact]
    public async Task CompleteRoute_WhenInProgress_ShouldCompleteRoute()
    {
        var route = CreateTestRoute(RouteStatus.InProgress);
        await _routeRepository.AddAsync(route);

        var useCase = new CompleteRouteUseCase(_routeRepository, _unitOfWork);
        var arrival = DateTimeOffset.UtcNow.AddHours(3);
        var result = await useCase.ExecuteAsync(new CompleteRouteCommand(route.Id, arrival));

        Assert.Equal(RouteStatus.Completed.ToString(), result.Status);
        Assert.Equal(arrival, result.ActualArrival);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task CancelRoute_WhenPlanned_ShouldCancelRoute()
    {
        var route = CreateTestRoute(RouteStatus.Planned);
        await _routeRepository.AddAsync(route);

        var useCase = new CancelRouteUseCase(_routeRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new CancelRouteCommand(route.Id, "Weather conditions"));

        Assert.Equal(RouteStatus.Cancelled.ToString(), result.Status);
        Assert.Equal("Weather conditions", result.CancellationReason);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _routeRepository.UpdateCallCount);
        Assert.True(_routeRepository.WasUpdated(route.Id));
    }

    [Fact]
    public async Task CancelRoute_WhenAlreadyCompleted_ShouldThrowInvalidRouteStateException()
    {
        var route = CreateTestRoute(RouteStatus.Completed);
        await _routeRepository.AddAsync(route);

        var useCase = new CancelRouteUseCase(_routeRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidRouteStateException>(() =>
            useCase.ExecuteAsync(new CancelRouteCommand(route.Id, "Reason")));
    }
}
