namespace FleetOps.Domain.Entities;

using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.Primitives;
using FleetOps.Domain.ValueObjects;

public sealed class Vehicle : AggregateRoot
{
    public LicensePlate LicensePlate { get; private set; }
    public VehicleType Type { get; private set; }
    public VehicleStatus Status { get; private set; }
    public string Make { get; private set; }
    public string Model { get; private set; }
    public int Year { get; private set; }
    public int Mileage { get; private set; }
    public decimal CapacityKg { get; private set; }
    public Guid? CurrentDriverId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Vehicle()
    {
        LicensePlate = null!;
        Make = null!;
        Model = null!;
    }

    private Vehicle(
        Guid id,
        LicensePlate licensePlate,
        VehicleType type,
        string make,
        string model,
        int year,
        int mileage,
        decimal capacityKg,
        DateTimeOffset createdAt) : base(id)
    {
        LicensePlate = licensePlate;
        Type = type;
        Status = VehicleStatus.Active;
        Make = make;
        Model = model;
        Year = year;
        Mileage = mileage;
        CapacityKg = capacityKg;
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public static Vehicle Create(
        Guid id,
        LicensePlate licensePlate,
        VehicleType type,
        string make,
        string model,
        int year,
        int mileage,
        decimal capacityKg)
    {
        ArgumentNullException.ThrowIfNull(licensePlate);

        if (string.IsNullOrWhiteSpace(make))
        {
            throw new DomainValidationException("Vehicle make cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            throw new DomainValidationException("Vehicle model cannot be empty.");
        }

        var currentYear = DateTimeOffset.UtcNow.Year;
        if (year < 1900 || year > currentYear + 1)
        {
            throw new DomainValidationException($"Vehicle year must be between 1900 and {currentYear + 1}.");
        }

        if (mileage < 0)
        {
            throw new DomainValidationException("Vehicle mileage cannot be negative.");
        }

        if (capacityKg < 0)
        {
            throw new DomainValidationException("Vehicle capacity cannot be negative.");
        }

        return new Vehicle(
            id,
            licensePlate,
            type,
            make.Trim(),
            model.Trim(),
            year,
            mileage,
            capacityKg,
            DateTimeOffset.UtcNow);
    }

    public void Activate()
    {
        if (Status == VehicleStatus.Active)
        {
            throw new InvalidVehicleStateException("Vehicle is already active.");
        }

        if (Status == VehicleStatus.UnderMaintenance)
        {
            throw new InvalidVehicleStateException("Cannot directly activate vehicle while it is under maintenance.");
        }

        Status = VehicleStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new VehicleActivatedDomainEvent(Id, UpdatedAt.Value));
    }

    public void Deactivate()
    {
        if (Status == VehicleStatus.Inactive)
        {
            throw new InvalidVehicleStateException("Vehicle is already inactive.");
        }

        if (Status == VehicleStatus.UnderMaintenance)
        {
            throw new InvalidVehicleStateException("Cannot deactivate vehicle while it is under maintenance.");
        }

        if (CurrentDriverId.HasValue)
        {
            var previousDriverId = CurrentDriverId.Value;
            CurrentDriverId = null;
            RaiseDomainEvent(new DriverUnassignedFromVehicleDomainEvent(Id, previousDriverId, DateTimeOffset.UtcNow));
        }

        Status = VehicleStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new VehicleDeactivatedDomainEvent(Id, UpdatedAt.Value));
    }

    public void AssignDriver(Guid driverId)
    {
        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("Driver identifier cannot be empty.");
        }

        if (Status != VehicleStatus.Active)
        {
            throw new InvalidVehicleStateException("Driver can only be assigned to an active vehicle.");
        }

        if (CurrentDriverId == driverId)
        {
            return;
        }

        UpdatedAt = DateTimeOffset.UtcNow;

        if (CurrentDriverId.HasValue)
        {
            var previousDriverId = CurrentDriverId.Value;
            RaiseDomainEvent(new DriverUnassignedFromVehicleDomainEvent(Id, previousDriverId, UpdatedAt.Value));
        }

        CurrentDriverId = driverId;

        RaiseDomainEvent(new DriverAssignedToVehicleDomainEvent(Id, driverId, UpdatedAt.Value));
    }

    public void UnassignDriver()
    {
        if (!CurrentDriverId.HasValue)
        {
            throw new InvalidVehicleStateException("Vehicle has no driver assigned.");
        }

        var previousDriverId = CurrentDriverId.Value;
        CurrentDriverId = null;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DriverUnassignedFromVehicleDomainEvent(Id, previousDriverId, UpdatedAt.Value));
    }

    public void UpdateMileage(int newMileage)
    {
        if (newMileage < Mileage)
        {
            throw new DomainValidationException("New mileage cannot be less than current mileage.");
        }

        if (newMileage == Mileage)
        {
            return;
        }

        var oldMileage = Mileage;
        Mileage = newMileage;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new VehicleMileageUpdatedDomainEvent(Id, oldMileage, newMileage, UpdatedAt.Value));
    }

    public void SendToMaintenance()
    {
        if (Status == VehicleStatus.UnderMaintenance)
        {
            throw new InvalidVehicleStateException("Vehicle is already under maintenance.");
        }

        if (CurrentDriverId.HasValue)
        {
            var previousDriverId = CurrentDriverId.Value;
            CurrentDriverId = null;
            RaiseDomainEvent(new DriverUnassignedFromVehicleDomainEvent(Id, previousDriverId, DateTimeOffset.UtcNow));
        }

        Status = VehicleStatus.UnderMaintenance;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new VehicleSentToMaintenanceDomainEvent(Id, UpdatedAt.Value));
    }

    public void ReturnFromMaintenance()
    {
        if (Status != VehicleStatus.UnderMaintenance)
        {
            throw new InvalidVehicleStateException("Vehicle is not under maintenance.");
        }

        Status = VehicleStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new VehicleReturnedFromMaintenanceDomainEvent(Id, UpdatedAt.Value));
    }
}
