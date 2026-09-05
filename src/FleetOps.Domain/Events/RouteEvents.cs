namespace FleetOps.Domain.Events;

public sealed record RouteStartedDomainEvent(
    Guid RouteId,
    DateTimeOffset ActualDeparture,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RouteCompletedDomainEvent(
    Guid RouteId,
    DateTimeOffset ActualArrival,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record RouteCancelledDomainEvent(
    Guid RouteId,
    string Reason,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryAddedToRouteDomainEvent(
    Guid RouteId,
    Guid DeliveryId,
    DateTimeOffset OccurredOn) : IDomainEvent;

public sealed record DeliveryRemovedFromRouteDomainEvent(
    Guid RouteId,
    Guid DeliveryId,
    DateTimeOffset OccurredOn) : IDomainEvent;
