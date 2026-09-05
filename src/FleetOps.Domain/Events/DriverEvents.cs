namespace FleetOps.Domain.Events;

public sealed record DriverActivatedDomainEvent(Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DriverSuspendedDomainEvent(Guid DriverId, string Reason, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DriverDeactivatedDomainEvent(Guid DriverId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleAssignedToDriverDomainEvent(Guid DriverId, Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record VehicleUnassignedFromDriverDomainEvent(Guid DriverId, Guid VehicleId, DateTimeOffset OccurredOn) : IDomainEvent;
