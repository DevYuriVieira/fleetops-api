namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;

public class RouteTests
{
    private static Route CreateValidRoute()
    {
        var origin = new Address("Hub A", "1", "Logistics Park", "City A", "SP", "01000-000", "Brazil");
        var destination = new Address("Hub B", "2", "Logistics Park", "City B", "RJ", "20000-000", "Brazil");
        var departure = DateTimeOffset.UtcNow.AddHours(1);
        var arrival = departure.AddHours(5);

        return Route.Create(Guid.NewGuid(), origin, destination, departure, arrival);
    }

    [Fact]
    public void Create_ShouldInstantiateRoute_WhenValid()
    {
        var id = Guid.NewGuid();
        var origin = new Address("Hub A", "1", "Logistics Park", "City A", "SP", "01000-000", "Brazil");
        var destination = new Address("Hub B", "2", "Logistics Park", "City B", "RJ", "20000-000", "Brazil");
        var departure = DateTimeOffset.UtcNow.AddHours(1);
        var arrival = departure.AddHours(4);

        var route = Route.Create(id, origin, destination, departure, arrival);

        Assert.Equal(id, route.Id);
        Assert.Equal(RouteStatus.Planned, route.Status);
        Assert.Empty(route.DeliveryIds);
        Assert.Null(route.AssignedVehicleId);
        Assert.Null(route.AssignedDriverId);
    }

    [Fact]
    public void Create_ShouldThrowValidationException_WhenArrivalIsBeforeOrEqualDeparture()
    {
        var origin = new Address("Hub A", "1", "Logistics Park", "City A", "SP", "01000-000", "Brazil");
        var destination = new Address("Hub B", "2", "Logistics Park", "City B", "RJ", "20000-000", "Brazil");
        var departure = DateTimeOffset.UtcNow.AddHours(5);
        var arrival = departure.AddHours(-1);

        Assert.Throws<DomainValidationException>(() => Route.Create(Guid.NewGuid(), origin, destination, departure, arrival));
    }

    [Fact]
    public void AddAndRemoveDelivery_ShouldUpdateDeliveryCollectionAndRaiseEvents()
    {
        var route = CreateValidRoute();
        var deliveryId = Guid.NewGuid();

        route.AddDelivery(deliveryId);
        Assert.Single(route.DeliveryIds);
        Assert.Contains(deliveryId, route.DeliveryIds);
        Assert.Contains(route.DomainEvents, e => e is DeliveryAddedToRouteDomainEvent);

        route.ClearDomainEvents();
        route.RemoveDelivery(deliveryId);
        Assert.Empty(route.DeliveryIds);
        Assert.Contains(route.DomainEvents, e => e is DeliveryRemovedFromRouteDomainEvent);
    }

    [Fact]
    public void Start_ShouldTransitionToInProgressAndRaiseEvent_WhenValid()
    {
        var route = CreateValidRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        route.AddDelivery(Guid.NewGuid());
        route.ClearDomainEvents();

        var departure = DateTimeOffset.UtcNow;
        route.Start(departure);

        Assert.Equal(RouteStatus.InProgress, route.Status);
        Assert.Equal(departure, route.ActualDeparture);
        Assert.Contains(route.DomainEvents, e => e is RouteStartedDomainEvent);
    }

    [Fact]
    public void Start_ShouldThrowInvalidStateException_WhenMissingVehicleOrDriverOrDeliveries()
    {
        var route = CreateValidRoute();

        Assert.Throws<InvalidRouteStateException>(() => route.Start(DateTimeOffset.UtcNow));

        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        Assert.Throws<InvalidRouteStateException>(() => route.Start(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Complete_ShouldTransitionToCompletedAndRaiseEvent()
    {
        var route = CreateValidRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        route.AddDelivery(Guid.NewGuid());
        var departure = DateTimeOffset.UtcNow;
        route.Start(departure);
        route.ClearDomainEvents();

        var arrival = departure.AddHours(4);
        route.Complete(arrival);

        Assert.Equal(RouteStatus.Completed, route.Status);
        Assert.Equal(arrival, route.ActualArrival);
        Assert.Contains(route.DomainEvents, e => e is RouteCompletedDomainEvent);
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelledAndRaiseEvent()
    {
        var route = CreateValidRoute();

        route.Cancel("Road closure");

        Assert.Equal(RouteStatus.Cancelled, route.Status);
        Assert.Equal("Road closure", route.CancellationReason);
        Assert.Contains(route.DomainEvents, e => e is RouteCancelledDomainEvent);
    }

    [Fact]
    public void Cancel_ShouldThrowInvalidStateException_WhenAlreadyCompleted()
    {
        var route = CreateValidRoute();
        route.Assign(Guid.NewGuid(), Guid.NewGuid());
        route.AddDelivery(Guid.NewGuid());
        var departure = DateTimeOffset.UtcNow;
        route.Start(departure);
        route.Complete(departure.AddHours(1));

        Assert.Throws<InvalidRouteStateException>(() => route.Cancel("Road closure"));
    }
}
