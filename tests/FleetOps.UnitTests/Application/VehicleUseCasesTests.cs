namespace FleetOps.UnitTests.Application;

using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Application.UseCases.Vehicles;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;
using FleetOps.UnitTests.Application.Doubles;

public sealed class VehicleUseCasesTests
{
    private readonly InMemoryVehicleRepository _vehicleRepository = new();
    private readonly InMemoryDriverRepository _driverRepository = new();
    private readonly InMemoryMaintenanceRepository _maintenanceRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();

    private Vehicle CreateTestVehicle(VehicleStatus status = VehicleStatus.Active, Guid? driverId = null, int mileage = 1000)
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC-1234"),
            VehicleType.Truck,
            "Volvo",
            "FH540",
            2023,
            mileage,
            25000m);

        if (driverId.HasValue)
        {
            vehicle.AssignDriver(driverId.Value);
        }

        if (status == VehicleStatus.Inactive)
        {
            vehicle.Deactivate();
        }
        else if (status == VehicleStatus.UnderMaintenance)
        {
            vehicle.SendToMaintenance();
        }

        return vehicle;
    }

    private Driver CreateTestDriver(DriverStatus status = DriverStatus.Active)
    {
        var driver = Driver.Create(
            Guid.NewGuid(),
            "John Doe",
            "DL-998877",
            "john.doe@fleetops.com",
            "+5511999998888");

        if (status == DriverStatus.Suspended)
        {
            driver.Suspend("License pending review");
        }
        else if (status == DriverStatus.Inactive)
        {
            driver.Deactivate();
        }

        return driver;
    }

    [Fact]
    public async Task RegisterVehicle_WithValidCommand_ShouldCreateAndPersistVehicle()
    {
        var useCase = new RegisterVehicleUseCase(_vehicleRepository, _unitOfWork);
        var command = new RegisterVehicleCommand(
            "BRA2E19",
            "Van",
            "Mercedes-Benz",
            "Sprinter",
            2024,
            5000,
            1500m);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("BRA2E19", result.LicensePlate);
        Assert.Equal(VehicleType.Van.ToString(), result.Type);
        Assert.Equal(VehicleStatus.Active.ToString(), result.Status);
        Assert.Equal("Mercedes-Benz", result.Make);
        Assert.Equal("Sprinter", result.Model);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var stored = await _vehicleRepository.GetByIdAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("BRA2E19", stored.LicensePlate.Value);
    }

    [Fact]
    public async Task RegisterVehicle_WithInvalidVehicleType_ShouldThrowValidationException()
    {
        var useCase = new RegisterVehicleUseCase(_vehicleRepository, _unitOfWork);
        var command = new RegisterVehicleCommand(
            "BRA2E19",
            "SpaceShip",
            "Mercedes-Benz",
            "Sprinter",
            2024,
            5000,
            1500m);

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task RegisterVehicle_WhenPlateAlreadyExists_ShouldThrowConflictException()
    {
        var existing = CreateTestVehicle();
        await _vehicleRepository.AddAsync(existing);

        var useCase = new RegisterVehicleUseCase(_vehicleRepository, _unitOfWork);
        var command = new RegisterVehicleCommand(
            existing.LicensePlate.Value,
            "Truck",
            "Scania",
            "R450",
            2022,
            50000,
            20000m);

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ActivateVehicle_WhenInactive_ShouldActivateVehicle()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Inactive);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new ActivateVehicleUseCase(_vehicleRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new ActivateVehicleCommand(vehicle.Id));

        Assert.Equal(VehicleStatus.Active.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task ActivateVehicle_WhenNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new ActivateVehicleUseCase(_vehicleRepository, _unitOfWork);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new ActivateVehicleCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task DeactivateVehicle_WhenActive_ShouldDeactivateVehicleAndUnassignDriver()
    {
        var driver = CreateTestDriver();
        await _driverRepository.AddAsync(driver);

        var vehicle = CreateTestVehicle(VehicleStatus.Active, driver.Id);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new DeactivateVehicleUseCase(_vehicleRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new DeactivateVehicleCommand(vehicle.Id));

        Assert.Equal(VehicleStatus.Inactive.ToString(), result.Status);
        Assert.Null(result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task DeactivateVehicle_WhenNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new DeactivateVehicleUseCase(_vehicleRepository, _unitOfWork);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new DeactivateVehicleCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenDriverActiveAndVehicleActive_ShouldAssign()
    {
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver();

        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, driver.Id));

        Assert.Equal(driver.Id, result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenDriverReassigned_ShouldUnassignPreviousAndAssignNewDriver()
    {
        var driverA = CreateTestDriver();
        var driverB = Driver.Create(
            Guid.NewGuid(),
            "Jane Smith",
            "DL-112233",
            "jane.smith@fleetops.com",
            "+5511977778888");

        await _driverRepository.AddAsync(driverA);
        await _driverRepository.AddAsync(driverB);

        var vehicle = CreateTestVehicle(VehicleStatus.Active, driverA.Id);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, driverB.Id));

        Assert.Equal(driverB.Id, result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenSameDriverAssignedTwice_ShouldBeIdempotent()
    {
        var driver = CreateTestDriver();
        await _driverRepository.AddAsync(driver);

        var vehicle = CreateTestVehicle(VehicleStatus.Active, driver.Id);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, driver.Id));

        Assert.Equal(driver.Id, result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenCancellationRequested_ShouldThrowOperationCanceledException()
    {
        var driver = CreateTestDriver();
        var vehicle = CreateTestVehicle();
        await _driverRepository.AddAsync(driver);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);
        var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, driver.Id), cts.Token));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenDriverNotActive_ShouldThrowConflictException()
    {
        var vehicle = CreateTestVehicle();
        var driver = CreateTestDriver(DriverStatus.Suspended);

        await _vehicleRepository.AddAsync(vehicle);
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, driver.Id)));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenVehicleNotFound_ShouldThrowNotFoundException()
    {
        var driver = CreateTestDriver();
        await _driverRepository.AddAsync(driver);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new AssignDriverToVehicleCommand(Guid.NewGuid(), driver.Id)));
    }

    [Fact]
    public async Task AssignDriverToVehicle_WhenDriverNotFound_ShouldThrowNotFoundException()
    {
        var vehicle = CreateTestVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new AssignDriverToVehicleUseCase(_vehicleRepository, _driverRepository, _unitOfWork);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            useCase.ExecuteAsync(new AssignDriverToVehicleCommand(vehicle.Id, Guid.NewGuid())));
    }

    [Fact]
    public async Task UnassignDriverFromVehicle_WhenDriverAssigned_ShouldUnassign()
    {
        var driver = CreateTestDriver();
        await _driverRepository.AddAsync(driver);

        var vehicle = CreateTestVehicle(VehicleStatus.Active, driver.Id);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new UnassignDriverFromVehicleUseCase(_vehicleRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new UnassignDriverFromVehicleCommand(vehicle.Id));

        Assert.Null(result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task UnassignDriverFromVehicle_WhenNoDriverAssigned_ShouldThrowInvalidVehicleStateException()
    {
        var vehicle = CreateTestVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new UnassignDriverFromVehicleUseCase(_vehicleRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidVehicleStateException>(() =>
            useCase.ExecuteAsync(new UnassignDriverFromVehicleCommand(vehicle.Id)));
    }

    [Fact]
    public async Task UpdateVehicleMileage_WithValidMileage_ShouldUpdate()
    {
        var vehicle = CreateTestVehicle(mileage: 1000);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new UpdateVehicleMileageUseCase(_vehicleRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new UpdateVehicleMileageCommand(vehicle.Id, 1500));

        Assert.Equal(1500, result.Mileage);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task UpdateVehicleMileage_WithLowerMileage_ShouldThrowDomainValidationException()
    {
        var vehicle = CreateTestVehicle(mileage: 1000);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new UpdateVehicleMileageUseCase(_vehicleRepository, _unitOfWork);

        await Assert.ThrowsAsync<DomainValidationException>(() =>
            useCase.ExecuteAsync(new UpdateVehicleMileageCommand(vehicle.Id, 900)));
    }

    [Fact]
    public async Task SendVehicleToMaintenance_WhenActive_ShouldTransitionToUnderMaintenance()
    {
        var driver = CreateTestDriver();
        await _driverRepository.AddAsync(driver);

        var vehicle = CreateTestVehicle(VehicleStatus.Active, driver.Id);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new SendVehicleToMaintenanceUseCase(_vehicleRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new SendVehicleToMaintenanceCommand(vehicle.Id));

        Assert.Equal(VehicleStatus.UnderMaintenance.ToString(), result.Status);
        Assert.Null(result.CurrentDriverId);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task ReturnVehicleFromMaintenance_WhenUnderMaintenanceAndNoActiveMaintenance_ShouldTransitionToActive()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.UnderMaintenance);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new ReturnVehicleFromMaintenanceUseCase(_vehicleRepository, _maintenanceRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new ReturnVehicleFromMaintenanceCommand(vehicle.Id));

        Assert.Equal(VehicleStatus.Active.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
        Assert.Equal(1, _vehicleRepository.UpdateCallCount);
        Assert.True(_vehicleRepository.WasUpdated(vehicle.Id));
    }

    [Fact]
    public async Task ReturnVehicleFromMaintenance_WhenActiveMaintenanceExists_ShouldThrowConflictException()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.UnderMaintenance);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            MaintenanceType.Corrective,
            "Engine repair",
            DateTimeOffset.UtcNow);
        maintenance.Start(DateTimeOffset.UtcNow);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new ReturnVehicleFromMaintenanceUseCase(_vehicleRepository, _maintenanceRepository, _unitOfWork);

        await Assert.ThrowsAsync<ConflictException>(() =>
            useCase.ExecuteAsync(new ReturnVehicleFromMaintenanceCommand(vehicle.Id)));
    }

    [Fact]
    public async Task ReturnVehicleFromMaintenance_WhenNotUnderMaintenance_ShouldThrowInvalidVehicleStateException()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Active);
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new ReturnVehicleFromMaintenanceUseCase(_vehicleRepository, _maintenanceRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidVehicleStateException>(() =>
            useCase.ExecuteAsync(new ReturnVehicleFromMaintenanceCommand(vehicle.Id)));
    }
}
