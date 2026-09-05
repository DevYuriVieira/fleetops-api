namespace FleetOps.Domain.Events;

public sealed record DeliveryAssignedDomainEvent(
    Guid DeliveryId,
    Guid VehicleId,
    Guid DriverId,
    DateTimeOffset EstimatedDeliveryTime,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryStartedDomainEvent(Guid DeliveryId, DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryCompletedDomainEvent(
    Guid DeliveryId,
    DateTimeOffset ActualDeliveryTime,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryCancelledDomainEvent(
    Guid DeliveryId,
    string Reason,
    DateTimeOffset OccurredOn) : IDomainEvent;
