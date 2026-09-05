namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;

public class DeliveryTests
{
    private static Delivery CreateValidDelivery()
    {
        var origin = new Address("Origin St", "10", "Industrial", "OriginCity", "SP", "01000-000", "Brazil");
        var destination = new Address("Dest St", "20", "Commercial", "DestCity", "RJ", "20000-000", "Brazil");

        return Delivery.Create(
            Guid.NewGuid(),
            TrackingCode.Create("TRK-123456"),
            origin,
            destination,
            DeliveryPriority.Standard,
            15.5m);
    }

    [Fact]
    public void Create_ShouldInstantiateDelivery_WhenValid()
    {
        var id = Guid.NewGuid();
        var code = TrackingCode.Create("TRK-999999");
        var origin = new Address("Origin St", "10", "Industrial", "OriginCity", "SP", "01000-000", "Brazil");
        var destination = new Address("Dest St", "20", "Commercial", "DestCity", "RJ", "20000-000", "Brazil");

        var delivery = Delivery.Create(id, code, origin, destination, DeliveryPriority.Urgent, 25.0m);

        Assert.Equal(id, delivery.Id);
        Assert.Equal(code, delivery.TrackingCode);
        Assert.Equal(DeliveryStatus.Pending, delivery.Status);
        Assert.Equal(DeliveryPriority.Urgent, delivery.Priority);
        Assert.Equal(25.0m, delivery.WeightKg);
        Assert.Null(delivery.AssignedVehicleId);
        Assert.Null(delivery.AssignedDriverId);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenWeightIsZeroOrNegative()
    {
        var origin = new Address("Origin St", "10", "Industrial", "OriginCity", "SP", "01000-000", "Brazil");
        var destination = new Address("Dest St", "20", "Commercial", "DestCity", "RJ", "20000-000", "Brazil");

        Assert.Throws<DomainValidationException>(() => Delivery.Create(
            Guid.NewGuid(),
            TrackingCode.Create("TRK-123456"),
            origin,
            destination,
            DeliveryPriority.Low,
            0m));
    }

    [Fact]
    public void Lifecycle_ShouldProgressThroughStagesCorrectly()
    {
        var delivery = CreateValidDelivery();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var estimated = DateTimeOffset.UtcNow.AddHours(2);

        delivery.Assign(vehicleId, driverId, estimated);
        Assert.Equal(DeliveryStatus.Assigned, delivery.Status);
        Assert.Equal(vehicleId, delivery.AssignedVehicleId);
        Assert.Equal(driverId, delivery.AssignedDriverId);
        Assert.Contains(delivery.DomainEvents, e => e is DeliveryAssignedDomainEvent);

        delivery.ClearDomainEvents();
        delivery.Start();
        Assert.Equal(DeliveryStatus.InTransit, delivery.Status);
        Assert.Contains(delivery.DomainEvents, e => e is DeliveryStartedDomainEvent);

        delivery.ClearDomainEvents();
        var actualDeliveryTime = DateTimeOffset.UtcNow;
        delivery.Complete(actualDeliveryTime);
        Assert.Equal(DeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(actualDeliveryTime, delivery.ActualDeliveryTime);
        Assert.Contains(delivery.DomainEvents, e => e is DeliveryCompletedDomainEvent);
    }

    [Fact]
    public void Start_ShouldThrowInvalidStateException_WhenNotAssigned()
    {
        var delivery = CreateValidDelivery();

        Assert.Throws<InvalidDeliveryStateException>(() => delivery.Start());
    }

    [Fact]
    public void Complete_ShouldThrowInvalidStateException_WhenNotInTransit()
    {
        var delivery = CreateValidDelivery();

        Assert.Throws<InvalidDeliveryStateException>(() => delivery.Complete(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelledAndRaiseEvent()
    {
        var delivery = CreateValidDelivery();

        delivery.Cancel("Customer requested cancellation");

        Assert.Equal(DeliveryStatus.Cancelled, delivery.Status);
        Assert.Equal("Customer requested cancellation", delivery.CancellationReason);
        Assert.Contains(delivery.DomainEvents, e => e is DeliveryCancelledDomainEvent);
    }

    [Fact]
    public void Cancel_ShouldThrowInvalidStateException_WhenAlreadyDelivered()
    {
        var delivery = CreateValidDelivery();
        delivery.Assign(Guid.NewGuid(), Guid.NewGuid(), DateTimeOffset.UtcNow.AddHours(1));
        delivery.Start();
        delivery.Complete(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidDeliveryStateException>(() => delivery.Cancel("Cannot cancel"));
    }
}
