namespace FleetOps.UnitTests.Application;

using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Application.UseCases.Maintenance;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;
using FleetOps.UnitTests.Application.Doubles;

public sealed class MaintenanceUseCasesTests
{
    private readonly InMemoryMaintenanceRepository _maintenanceRepository = new();
    private readonly InMemoryVehicleRepository _vehicleRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();

    private Vehicle CreateTestVehicle(VehicleStatus status = VehicleStatus.Active)
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC-1234"),
            VehicleType.Truck,
            "Volvo",
            "FH540",
            2023,
            50000,
            25000m);

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

    private Maintenance CreateTestMaintenance(
        Guid vehicleId,
        MaintenanceStatus status = MaintenanceStatus.Scheduled)
    {
        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicleId,
            MaintenanceType.Preventive,
            "Oil change and inspection",
            DateTimeOffset.UtcNow.AddDays(1));

        if (status == MaintenanceStatus.InProgress || status == MaintenanceStatus.Completed)
        {
            maintenance.Start(DateTimeOffset.UtcNow);
        }

        if (status == MaintenanceStatus.Completed)
        {
            maintenance.Complete(DateTimeOffset.UtcNow.AddHours(2), new Money(500m, "USD"));
        }
        else if (status == MaintenanceStatus.Cancelled)
        {
            maintenance.Cancel("Cancelled for test");
        }

        return maintenance;
    }

    [Fact]
    public async Task ScheduleMaintenance_WithValidCommand_ShouldCreateAndPersistMaintenance()
    {
        var vehicle = CreateTestVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new ScheduleMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var scheduledTime = DateTimeOffset.UtcNow.AddDays(3);
        var command = new ScheduleMaintenanceCommand(
            vehicle.Id,
            "Preventive",
            "Periodic brake inspection",
            scheduledTime);

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal(vehicle.Id, result.VehicleId);
        Assert.Equal(MaintenanceType.Preventive.ToString(), result.Type);
        Assert.Equal(MaintenanceStatus.Scheduled.ToString(), result.Status);
        Assert.Equal("Periodic brake inspection", result.Description);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var stored = await _maintenanceRepository.GetByIdAsync(result.Id);
        Assert.NotNull(stored);
    }

    [Fact]
    public async Task ScheduleMaintenance_WhenVehicleNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new ScheduleMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new ScheduleMaintenanceCommand(
            Guid.NewGuid(),
            "Preventive",
            "Brake inspection",
            DateTimeOffset.UtcNow.AddDays(1));

        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ScheduleMaintenance_WithInvalidType_ShouldThrowValidationException()
    {
        var vehicle = CreateTestVehicle();
        await _vehicleRepository.AddAsync(vehicle);

        var useCase = new ScheduleMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new ScheduleMaintenanceCommand(
            vehicle.Id,
            "NonExistingType",
            "Brake inspection",
            DateTimeOffset.UtcNow.AddDays(1));

        await Assert.ThrowsAsync<ValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task StartMaintenance_WhenScheduled_ShouldTransitionToInProgressAndSendVehicleToMaintenance()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Active);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.Scheduled);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new StartMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var startTime = DateTimeOffset.UtcNow;
        var result = await useCase.ExecuteAsync(new StartMaintenanceCommand(maintenance.Id, startTime));

        Assert.Equal(MaintenanceStatus.InProgress.ToString(), result.Status);
        Assert.Equal(startTime, result.StartedAt);
        Assert.Equal(VehicleStatus.UnderMaintenance, vehicle.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task StartMaintenance_WhenAlreadyInProgress_ShouldThrowInvalidMaintenanceStateException()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.UnderMaintenance);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.InProgress);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new StartMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);

        await Assert.ThrowsAsync<InvalidMaintenanceStateException>(() =>
            useCase.ExecuteAsync(new StartMaintenanceCommand(maintenance.Id)));
    }

    [Fact]
    public async Task CompleteMaintenance_WhenInProgress_ShouldCompleteAndReturnVehicleToActive()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.UnderMaintenance);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.InProgress);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new CompleteMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var completedTime = DateTimeOffset.UtcNow.AddHours(2);
        var command = new CompleteMaintenanceCommand(maintenance.Id, 1250.50m, "USD", completedTime, ReturnVehicleToActive: true);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(MaintenanceStatus.Completed.ToString(), result.Status);
        Assert.Equal(1250.50m, result.CostAmount);
        Assert.Equal("USD", result.CostCurrency);
        Assert.Equal(completedTime, result.CompletedAt);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CompleteMaintenance_WhenScheduled_ShouldThrowInvalidMaintenanceStateException()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Active);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.Scheduled);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new CompleteMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new CompleteMaintenanceCommand(maintenance.Id, 500m, "USD");

        await Assert.ThrowsAsync<InvalidMaintenanceStateException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task CancelMaintenance_WhenScheduled_ShouldCancelMaintenance()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Active);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.Scheduled);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new CancelMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new CancelMaintenanceCommand(maintenance.Id, "Scheduled by mistake");

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(MaintenanceStatus.Cancelled.ToString(), result.Status);
        Assert.Equal("Scheduled by mistake", result.CancellationReason);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelMaintenance_WhenInProgressAndReturnVehicleRequested_ShouldReturnVehicleToActive()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.UnderMaintenance);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.InProgress);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new CancelMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new CancelMaintenanceCommand(maintenance.Id, "Shop unable to complete", ReturnVehicleToActive: true);

        var result = await useCase.ExecuteAsync(command);

        Assert.Equal(MaintenanceStatus.Cancelled.ToString(), result.Status);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task CancelMaintenance_WhenAlreadyCompleted_ShouldThrowInvalidMaintenanceStateException()
    {
        var vehicle = CreateTestVehicle(VehicleStatus.Active);
        await _vehicleRepository.AddAsync(vehicle);

        var maintenance = CreateTestMaintenance(vehicle.Id, MaintenanceStatus.Completed);
        await _maintenanceRepository.AddAsync(maintenance);

        var useCase = new CancelMaintenanceUseCase(_maintenanceRepository, _vehicleRepository, _unitOfWork);
        var command = new CancelMaintenanceCommand(maintenance.Id, "Cannot cancel completed");

        await Assert.ThrowsAsync<InvalidMaintenanceStateException>(() => useCase.ExecuteAsync(command));
    }
}
