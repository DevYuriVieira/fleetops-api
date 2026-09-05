namespace FleetOps.UnitTests.Application;

using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Application.UseCases.Deliveries;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;
using FleetOps.UnitTests.Application.Doubles;

public sealed class DeliveryUseCasesTests
{
    private readonly InMemoryDeliveryRepository _deliveryRepository = new();
    private readonly InMemoryVehicleRepository _vehicleRepository = new();
    private readonly InMemoryDriverRepository _driverRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();

    private static AddressDto CreateSampleAddressDto(string street = "Av. Paulista", string number = "1000") =>
        new(street, number, "Bela Vista", "São Paulo", "SP", "01310-100", "Brasil");

    private Delivery CreateTestDelivery(
        DeliveryStatus status = DeliveryStatus.Pending,
        string tracking = "BR123456789XP",
        decimal weightKg = 50m)
    {
        var origin = new Address("Rua A", "10", "Centro", "São Paulo", "SP", "01001-000", "Brasil");
        var destination = new Address("Rua B", "20", "Jardins", "São Paulo", "SP", "01401-000", "Brasil");

        var delivery = Delivery.Create(
            Guid.NewGuid(),
            TrackingCode.Create(tracking),
            origin,
            destination,
            DeliveryPriority.Standard,
            weightKg);

        if (status == DeliveryStatus.Assigned || status == DeliveryStatus.InTransit || status == DeliveryStatus.Delivered)
        {
            delivery.Assign(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(4));
        }

        if (status == DeliveryStatus.InTransit || status == DeliveryStatus.Delivered)
        {
            delivery.Start();
        }

        if (status == DeliveryStatus.Delivered)
        {
            delivery.Complete(DateTimeOffset.UtcNow);
        }
        else if (status == DeliveryStatus.Cancelled)
        {
            delivery.Cancel("Cancelled for test");
        }

        return delivery;
    }

    private Vehicle CreateTestVehicle(VehicleStatus status = VehicleStatus.Active, decimal capacityKg = 1000m)
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC-1234"),
            VehicleType.Van,
            "Ford",
            "Transit",
            2023,
            15000,
            capacityKg);

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
            "Pedro Alvares",
            "CNH55443322",
            "pedro@fleetops.com",
            "+5511955554444");

        if (status == DriverStatus.Suspended)
        {
            driver.Suspend("Suspended");
        }

        return driver;
    }

    [Fact]
    public async Task CreateDelivery_WithValidCommand_ShouldCreateAndPersistDelivery()
    {
        var useCase = new CreateDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var command = new CreateDeliveryCommand(
            "BR998877665XP",
            CreateSampleAddressDto("Rua das Flores", "100"),
            CreateSampleAddressDto("Av. Brasil", "200"),
            "Urgent",
            45.5m);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("BR998877665XP", result.TrackingCode);
        Assert.Equal(DeliveryStatus.Pending.ToString(), result.Status);
        Assert.Equal(DeliveryPriority.Urgent.ToString(), result.Priority);
        Assert.Equal(45.5m, result.WeightKg);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var stored = await _deliveryRepository.GetByIdAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("BR998877665XP", stored.TrackingCode.Value);
    }

    [Fact]
    public async Task CreateDelivery_WithInvalidPriority_ShouldThrowValidationException()
    {
        var useCase = new CreateDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var command = new CreateDeliveryCommand(
            "BR998877665XP",
            CreateSampleAddressDto(),
            CreateSampleAddressDto(),
            "UltraSuperFast",
            10m);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CreateDelivery_WhenTrackingCodeAlreadyExists_ShouldThrowConflictException()
    {
        var existing = CreateTestDelivery();
        await _deliveryRepository.AddAsync(existing);

        var useCase = new CreateDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var command = new CreateDeliveryCommand(
            existing.TrackingCode.Value,
            CreateSampleAddressDto(),
            CreateSampleAddressDto(),
            "Standard",
            20m);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AssignDelivery_WithValidActiveVehicleAndDriver_ShouldAssignSuccessfully()
    {
        var delivery = CreateTestDelivery();
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver();

        await _deliveryRepository.AddAsync(delivery);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDeliveryUseCase(_deliveryRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var eta = DateTimeOffset.UtcNow.AddHours(2);
        var command = new AssignDeliveryCommand(delivery.Id, vehicle.Id, driver.Id, eta);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(DeliveryStatus.Assigned.ToString(), result.Status);
        Assert.Equal(vehicle.Id, result.AssignedVehicleId);
        Assert.Equal(driver.Id, result.AssignedDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignDelivery_WhenVehicleCapacityInsufficient_ShouldThrowConflictException()
    {
        var delivery = CreateTestDelivery(weightKg: 500m);
        var vehicle = CreateTestVehicle(capacityKg: 300m);
        var driver = CreateTestDriver();

        await _deliveryRepository.AddAsync(delivery);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDeliveryUseCase(_deliveryRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignDeliveryCommand(delivery.Id, vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AssignDelivery_WhenVehicleInactive_ShouldThrowConflictException()
    {
        var delivery = CreateTestDelivery();
        var vehicle = CreateTestVehicle(VehicleStatus.Inactive);
        var driver = CreateTestDriver();

        await _deliveryRepository.AddAsync(delivery);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDeliveryUseCase(_deliveryRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignDeliveryCommand(delivery.Id, vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task AssignDelivery_WhenDriverSuspended_ShouldThrowConflictException()
    {
        var delivery = CreateTestDelivery();
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver(DriverStatus.Suspended);

        await _deliveryRepository.AddAsync(delivery);
        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDeliveryUseCase(_deliveryRepository, _vehicleRepository, _driverRepository, _unitOfWork);
        var command = new AssignDeliveryCommand(delivery.Id, vehicle.Id, driver.Id, DateTimeOffset.UtcNow.AddHours(2));

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task StartDelivery_WhenAssigned_ShouldTransitionToInTransit()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.Assigned);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new StartDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new StartDeliveryCommand(delivery.Id));

        Assert.Equal(DeliveryStatus.InTransit.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartDelivery_WhenPending_ShouldThrowInvalidDeliveryStateException()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.Pending);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new StartDeliveryUseCase(_deliveryRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidDeliveryStateException>(() =>
            useCase.ExecuteAsync(new StartDeliveryCommand(delivery.Id)));
    }

    [Fact]
    public async Task CompleteDelivery_WhenInTransit_ShouldTransitionToDelivered()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.InTransit);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new CompleteDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var deliveryTime = DateTimeOffset.UtcNow;
        var result = await useCase.ExecuteAsync(new CompleteDeliveryCommand(delivery.Id, deliveryTime));

        Assert.Equal(DeliveryStatus.Delivered.ToString(), result.Status);
        Assert.Equal(deliveryTime, result.ActualDeliveryTime);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CompleteDelivery_WhenNotStarted_ShouldThrowInvalidDeliveryStateException()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.Pending);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new CompleteDeliveryUseCase(_deliveryRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidDeliveryStateException>(() =>
            useCase.ExecuteAsync(new CompleteDeliveryCommand(delivery.Id)));
    }

    [Fact]
    public async Task CancelDelivery_WhenPending_ShouldTransitionToCancelled()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.Pending);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new CancelDeliveryUseCase(_deliveryRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new CancelDeliveryCommand(delivery.Id, "Customer requested cancellation"));

        Assert.Equal(DeliveryStatus.Cancelled.ToString(), result.Status);
        Assert.Equal("Customer requested cancellation", result.CancellationReason);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelDelivery_WhenDelivered_ShouldThrowInvalidDeliveryStateException()
    {
        var delivery = CreateTestDelivery(DeliveryStatus.Delivered);
        await _deliveryRepository.AddAsync(delivery);

        var useCase = new CancelDeliveryUseCase(_deliveryRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidDeliveryStateException>(() =>
            useCase.ExecuteAsync(new CancelDeliveryCommand(delivery.Id, "Customer request")));
    }
}
