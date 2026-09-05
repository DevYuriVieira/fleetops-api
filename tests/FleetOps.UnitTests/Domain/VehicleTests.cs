namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;

public class VehicleTests
{
    private static Vehicle CreateValidVehicle()
    {
        return Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC1234"),
            VehicleType.Truck,
            "Volvo",
            "FH540",
            2023,
            50000,
            25000m);
    }

    [Fact]
    public void Create_ShouldInstantiateVehicle_WhenDataIsValid()
    {
        var id = Guid.NewGuid();
        var plate = LicensePlate.Create("XYZ9876");

        var vehicle = Vehicle.Create(
            id,
            plate,
            VehicleType.Van,
            "Mercedes-Benz",
            "Sprinter",
            2022,
            12000,
            1500m);

        Assert.Equal(id, vehicle.Id);
        Assert.Equal(plate, vehicle.LicensePlate);
        Assert.Equal(VehicleType.Van, vehicle.Type);
        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Equal("Mercedes-Benz", vehicle.Make);
        Assert.Equal("Sprinter", vehicle.Model);
        Assert.Equal(2022, vehicle.Year);
        Assert.Equal(12000, vehicle.Mileage);
        Assert.Equal(1500m, vehicle.CapacityKg);
        Assert.Null(vehicle.CurrentDriverId);
    }

    [Theory]
    [InlineData("", "Model")]
    [InlineData("Make", "")]
    public void Create_ShouldThrowValidationException_WhenMakeOrModelIsEmpty(string make, string model)
    {
        Assert.Throws<DomainValidationException>(() => Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC1234"),
            VehicleType.Car,
            make,
            model,
            2022,
            1000,
            500m));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenYearIsInvalid()
    {
        Assert.Throws<DomainValidationException>(() => Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC1234"),
            VehicleType.Car,
            "Toyota",
            "Corolla",
            1899,
            1000,
            500m));
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenMileageIsNegative()
    {
        Assert.Throws<DomainValidationException>(() => Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("ABC1234"),
            VehicleType.Car,
            "Toyota",
            "Corolla",
            2022,
            -10,
            500m));
    }

    [Fact]
    public void Deactivate_ShouldChangeStatusToInactiveAndRaiseDomainEvent()
    {
        var vehicle = CreateValidVehicle();
        vehicle.ClearDomainEvents();

        vehicle.Deactivate();

        Assert.Equal(VehicleStatus.Inactive, vehicle.Status);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleDeactivatedDomainEvent);
    }

    [Fact]
    public void Deactivate_ShouldThrowInvalidStateException_WhenAlreadyInactive()
    {
        var vehicle = CreateValidVehicle();
        vehicle.Deactivate();

        Assert.Throws<InvalidVehicleStateException>(() => vehicle.Deactivate());
    }

    [Fact]
    public void Activate_ShouldChangeStatusToActiveAndRaiseDomainEvent()
    {
        var vehicle = CreateValidVehicle();
        vehicle.Deactivate();
        vehicle.ClearDomainEvents();

        vehicle.Activate();

        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleActivatedDomainEvent);
    }

    [Fact]
    public void Activate_ShouldThrowInvalidStateException_WhenAlreadyActive()
    {
        var vehicle = CreateValidVehicle();

        Assert.Throws<InvalidVehicleStateException>(() => vehicle.Activate());
    }

    [Fact]
    public void AssignDriver_ShouldSetCurrentDriverAndRaiseDomainEvent()
    {
        var vehicle = CreateValidVehicle();
        var driverId = Guid.NewGuid();

        vehicle.AssignDriver(driverId);

        Assert.Equal(driverId, vehicle.CurrentDriverId);
        Assert.Contains(vehicle.DomainEvents, e => e is DriverAssignedToVehicleDomainEvent);
    }

    [Fact]
    public void AssignDriver_ShouldThrowInvalidStateException_WhenVehicleIsInactive()
    {
        var vehicle = CreateValidVehicle();
        vehicle.Deactivate();

        Assert.Throws<InvalidVehicleStateException>(() => vehicle.AssignDriver(Guid.NewGuid()));
    }

    [Fact]
    public void UnassignDriver_ShouldClearDriverAndRaiseDomainEvent()
    {
        var vehicle = CreateValidVehicle();
        var driverId = Guid.NewGuid();
        vehicle.AssignDriver(driverId);
        vehicle.ClearDomainEvents();

        vehicle.UnassignDriver();

        Assert.Null(vehicle.CurrentDriverId);
        Assert.Contains(vehicle.DomainEvents, e => e is DriverUnassignedFromVehicleDomainEvent);
    }

    [Fact]
    public void UpdateMileage_ShouldUpdateMileageAndRaiseEvent_WhenMileageIncreases()
    {
        var vehicle = CreateValidVehicle();
        vehicle.ClearDomainEvents();

        vehicle.UpdateMileage(55000);

        Assert.Equal(55000, vehicle.Mileage);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleMileageUpdatedDomainEvent);
    }

    [Fact]
    public void UpdateMileage_ShouldThrowValidationException_WhenNewMileageIsLower()
    {
        var vehicle = CreateValidVehicle();

        Assert.Throws<DomainValidationException>(() => vehicle.UpdateMileage(45000));
    }

    [Fact]
    public void MaintenanceTransitions_ShouldFunctionCorrectly()
    {
        var vehicle = CreateValidVehicle();
        var driverId = Guid.NewGuid();
        vehicle.AssignDriver(driverId);

        vehicle.SendToMaintenance();

        Assert.Equal(VehicleStatus.UnderMaintenance, vehicle.Status);
        Assert.Null(vehicle.CurrentDriverId);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleSentToMaintenanceDomainEvent);

        vehicle.ClearDomainEvents();
        vehicle.ReturnFromMaintenance();

        Assert.Equal(VehicleStatus.Active, vehicle.Status);
        Assert.Contains(vehicle.DomainEvents, e => e is VehicleReturnedFromMaintenanceDomainEvent);
    }
}
