namespace FleetOps.Domain.Events;

using FleetOps.Domain.ValueObjects;

public sealed record MaintenanceStartedDomainEvent(
    Guid MaintenanceId,
    Guid VehicleId,
    DateTimeOffset StartedAt,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MaintenanceCompletedDomainEvent(
    Guid MaintenanceId,
    Guid VehicleId,
    Money Cost,
    DateTimeOffset CompletedAt,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record MaintenanceCancelledDomainEvent(
    Guid MaintenanceId,
    Guid VehicleId,
    string Reason,
    DateTimeOffset OccurredOn) : IDomainEvent;
