namespace FleetOps.Domain.Entities;

using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.Primitives;
using FleetOps.Domain.ValueObjects;

public sealed class Route : AggregateRoot
{
    private readonly List<Guid> _deliveryIds = [];

    public Address Origin { get; private set; }
    public Address Destination { get; private set; }
    public RouteStatus Status { get; private set; }
    public DateTimeOffset PlannedDeparture { get; private set; }
    public DateTimeOffset? ActualDeparture { get; private set; }
    public DateTimeOffset EstimatedArrival { get; private set; }
    public DateTimeOffset? ActualArrival { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public string? CancellationReason { get; private set; }
    public IReadOnlyCollection<Guid> DeliveryIds => _deliveryIds.AsReadOnly();
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Route()
    {
        Origin = null!;
        Destination = null!;
    }

    private Route(
        Guid id,
        Address origin,
        Address destination,
        DateTimeOffset plannedDeparture,
        DateTimeOffset estimatedArrival,
        DateTimeOffset createdAt) : base(id)
    {
        Origin = origin;
        Destination = destination;
        PlannedDeparture = plannedDeparture;
        EstimatedArrival = estimatedArrival;
        Status = RouteStatus.Planned;
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public static Route Create(
        Guid id,
        Address origin,
        Address destination,
        DateTimeOffset plannedDeparture,
        DateTimeOffset estimatedArrival)
    {
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        if (estimatedArrival <= plannedDeparture)
        {
            throw new DomainValidationException("Estimated arrival must be after planned departure.");
        }

        return new Route(
            id,
            origin,
            destination,
            plannedDeparture,
            estimatedArrival,
            DateTimeOffset.UtcNow);
    }

    public void Assign(Guid vehicleId, Guid driverId)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("Vehicle identifier cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("Driver identifier cannot be empty.");
        }

        if (Status != RouteStatus.Planned)
        {
            throw new InvalidRouteStateException("Route vehicle and driver can only be assigned while in Planned status.");
        }

        AssignedVehicleId = vehicleId;
        AssignedDriverId = driverId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void AddDelivery(Guid deliveryId)
    {
        if (deliveryId == Guid.Empty)
        {
            throw new DomainValidationException("Delivery identifier cannot be empty.");
        }

        if (Status != RouteStatus.Planned)
        {
            throw new InvalidRouteStateException("Deliveries can only be added while route is in Planned status.");
        }

        if (_deliveryIds.Contains(deliveryId))
        {
            return;
        }

        _deliveryIds.Add(deliveryId);
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryAddedToRouteDomainEvent(Id, deliveryId, UpdatedAt.Value));
    }

    public void RemoveDelivery(Guid deliveryId)
    {
        if (Status != RouteStatus.Planned)
        {
            throw new InvalidRouteStateException("Deliveries can only be removed while route is in Planned status.");
        }

        if (!_deliveryIds.Remove(deliveryId))
        {
            throw new InvalidRouteStateException("Delivery is not associated with this route.");
        }

        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryRemovedFromRouteDomainEvent(Id, deliveryId, UpdatedAt.Value));
    }

    public void Start(DateTimeOffset actualDeparture)
    {
        if (Status != RouteStatus.Planned)
        {
            throw new InvalidRouteStateException("Route can only be started when in Planned status.");
        }

        if (!AssignedVehicleId.HasValue || !AssignedDriverId.HasValue)
        {
            throw new InvalidRouteStateException("Route cannot start without both an assigned vehicle and driver.");
        }

        if (_deliveryIds.Count == 0)
        {
            throw new InvalidRouteStateException("Route cannot start without at least one delivery.");
        }

        ActualDeparture = actualDeparture;
        Status = RouteStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new RouteStartedDomainEvent(Id, actualDeparture, UpdatedAt.Value));
    }

    public void Complete(DateTimeOffset actualArrival)
    {
        if (Status != RouteStatus.InProgress)
        {
            throw new InvalidRouteStateException("Route can only be completed when InProgress.");
        }

        if (ActualDeparture.HasValue && actualArrival < ActualDeparture.Value)
        {
            throw new DomainValidationException("Actual arrival cannot be before actual departure.");
        }

        ActualArrival = actualArrival;
        Status = RouteStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new RouteCompletedDomainEvent(Id, actualArrival, UpdatedAt.Value));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Cancellation reason cannot be empty.");
        }

        if (Status == RouteStatus.Completed)
        {
            throw new InvalidRouteStateException("Cannot cancel an already completed route.");
        }

        if (Status == RouteStatus.Cancelled)
        {
            throw new InvalidRouteStateException("Route is already cancelled.");
        }

        Status = RouteStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new RouteCancelledDomainEvent(Id, CancellationReason, UpdatedAt.Value));
    }
}
