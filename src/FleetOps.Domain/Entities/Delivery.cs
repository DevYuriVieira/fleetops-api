namespace FleetOps.Domain.Entities;

using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.Primitives;
using FleetOps.Domain.ValueObjects;

public sealed class Delivery : AggregateRoot
{
    public TrackingCode TrackingCode { get; private set; }
    public Address Origin { get; private set; }
    public Address Destination { get; private set; }
    public DeliveryStatus Status { get; private set; }
    public DeliveryPriority Priority { get; private set; }
    public decimal WeightKg { get; private set; }
    public Guid? AssignedVehicleId { get; private set; }
    public Guid? AssignedDriverId { get; private set; }
    public DateTimeOffset? EstimatedDeliveryTime { get; private set; }
    public DateTimeOffset? ActualDeliveryTime { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Delivery()
    {
        TrackingCode = null!;
        Origin = null!;
        Destination = null!;
    }

    private Delivery(
        Guid id,
        TrackingCode trackingCode,
        Address origin,
        Address destination,
        DeliveryPriority priority,
        decimal weightKg,
        DateTimeOffset createdAt) : base(id)
    {
        TrackingCode = trackingCode;
        Origin = origin;
        Destination = destination;
        Status = DeliveryStatus.Pending;
        Priority = priority;
        WeightKg = weightKg;
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public static Delivery Create(
        Guid id,
        TrackingCode trackingCode,
        Address origin,
        Address destination,
        DeliveryPriority priority,
        decimal weightKg)
    {
        ArgumentNullException.ThrowIfNull(trackingCode);
        ArgumentNullException.ThrowIfNull(origin);
        ArgumentNullException.ThrowIfNull(destination);

        if (weightKg <= 0)
        {
            throw new DomainValidationException("Delivery weight must be greater than zero.");
        }

        return new Delivery(
            id,
            trackingCode,
            origin,
            destination,
            priority,
            weightKg,
            DateTimeOffset.UtcNow);
    }

    public void Assign(Guid vehicleId, Guid driverId, DateTimeOffset estimatedDeliveryTime)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("Vehicle identifier cannot be empty.");
        }

        if (driverId == Guid.Empty)
        {
            throw new DomainValidationException("Driver identifier cannot be empty.");
        }

        if (Status is not (DeliveryStatus.Pending or DeliveryStatus.Assigned))
        {
            throw new InvalidDeliveryStateException($"Cannot assign delivery while in {Status} status.");
        }

        AssignedVehicleId = vehicleId;
        AssignedDriverId = driverId;
        EstimatedDeliveryTime = estimatedDeliveryTime;
        Status = DeliveryStatus.Assigned;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryAssignedDomainEvent(Id, vehicleId, driverId, estimatedDeliveryTime, UpdatedAt.Value));
    }

    public void Start()
    {
        if (Status != DeliveryStatus.Assigned)
        {
            throw new InvalidDeliveryStateException("Delivery can only be started when in Assigned status.");
        }

        Status = DeliveryStatus.InTransit;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryStartedDomainEvent(Id, UpdatedAt.Value));
    }

    public void Complete(DateTimeOffset actualDeliveryTime)
    {
        if (Status != DeliveryStatus.InTransit)
        {
            throw new InvalidDeliveryStateException("Delivery can only be completed when InTransit.");
        }

        ActualDeliveryTime = actualDeliveryTime;
        Status = DeliveryStatus.Delivered;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryCompletedDomainEvent(Id, actualDeliveryTime, UpdatedAt.Value));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Cancellation reason cannot be empty.");
        }

        if (Status == DeliveryStatus.Delivered)
        {
            throw new InvalidDeliveryStateException("Cannot cancel a delivery that has already been delivered.");
        }

        if (Status == DeliveryStatus.Cancelled)
        {
            throw new InvalidDeliveryStateException("Delivery is already cancelled.");
        }

        Status = DeliveryStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DeliveryCancelledDomainEvent(Id, CancellationReason, UpdatedAt.Value));
    }
}
