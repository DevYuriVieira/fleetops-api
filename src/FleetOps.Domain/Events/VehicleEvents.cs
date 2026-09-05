namespace FleetOps.Domain.Events;

public sealed record VehicleActivatedDomainEvent(Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleDeactivatedDomainEvent(Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DriverAssignedToVehicleDomainEvent(Guid VehicleId, Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DriverUnassignedFromVehicleDomainEvent(Guid VehicleId, Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleMileageUpdatedDomainEvent(Guid VehicleId, int OldMileage, int NewMileage, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleSentToMaintenanceDomainEvent(Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleReturnedFromMaintenanceDomainEvent(Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;
