using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Persistence;

public class RoutePersistenceTests : BaseIntegrationTest
{
    [Fact]
    public async Task CreateAndRetrieveRoute_ShouldPersistAddressesAndDeliveryIdList()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var origin = new Address("Origin Depot", "1", "Industrial", "Boston", "MA", "02101", "USA");
        var destination = new Address("Destination Hub", "2", "Logistics Park", "Worcester", "MA", "01601", "USA");
        var plannedStart = DateTimeOffset.UtcNow.AddDays(1);
        var plannedEnd = plannedStart.AddHours(4);

        var deliveryId1 = Guid.NewGuid();
        var deliveryId2 = Guid.NewGuid();
        var deliveryId3 = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var repo = new RouteRepository(context);
            var uow = CreateUnitOfWork(context);

            var route = Route.Create(routeId, origin, destination, plannedStart, plannedEnd);
            route.AddDelivery(deliveryId1);
            route.AddDelivery(deliveryId2);
            route.AddDelivery(deliveryId3);

            await repo.AddAsync(route);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new RouteRepository(context);
            var retrieved = await repo.GetByIdAsync(routeId);

            Assert.NotNull(retrieved);
            Assert.Equal(routeId, retrieved.Id);
            Assert.Equal(RouteStatus.Planned, retrieved.Status);
            Assert.True(Math.Abs((plannedStart - retrieved.PlannedDeparture).TotalMilliseconds) < 1);
            Assert.True(Math.Abs((plannedEnd - retrieved.EstimatedArrival).TotalMilliseconds) < 1);

            // Addresses
            Assert.Equal("Origin Depot", retrieved.Origin.Street);
            Assert.Equal("Boston", retrieved.Origin.City);
            Assert.Equal("Destination Hub", retrieved.Destination.Street);
            Assert.Equal("Worcester", retrieved.Destination.City);

            // Deliveries collection (mapped to postgres uuid[])
            Assert.Equal(3, retrieved.DeliveryIds.Count);
            Assert.Contains(deliveryId1, retrieved.DeliveryIds);
            Assert.Contains(deliveryId2, retrieved.DeliveryIds);
            Assert.Contains(deliveryId3, retrieved.DeliveryIds);
        }
    }

    [Fact]
    public async Task UpdateRoute_ShouldPersistDeliveryRemovalAndExecutionLifecycle()
    {
        // Arrange
        var routeId = Guid.NewGuid();
        var origin = new Address("Start St", "10", "South", "City", "ST", "00000", "USA");
        var destination = new Address("End St", "20", "North", "City", "ST", "00000", "USA");
        var plannedStart = DateTimeOffset.UtcNow.AddHours(1);
        var plannedEnd = plannedStart.AddHours(3);

        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var deliveryId1 = Guid.NewGuid();
        var deliveryId2 = Guid.NewGuid();

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var dRepo = new DriverRepository(context);
            var repo = new RouteRepository(context);
            var uow = CreateUnitOfWork(context);

            var vehicle = Vehicle.Create(
                vehicleId, LicensePlate.Create("RTE0001"), VehicleType.Truck, "Make", "Model", 2022, 1000, 10000m);
            var driver = Driver.Create(
                driverId, "Driver Route", "DL77777777", "route@test.com", "+123456789");

            var route = Route.Create(routeId, origin, destination, plannedStart, plannedEnd);
            route.AddDelivery(deliveryId1);
            route.AddDelivery(deliveryId2);
            route.Assign(vehicleId, driverId);

            await vRepo.AddAsync(vehicle);
            await dRepo.AddAsync(driver);
            await repo.AddAsync(route);
            await uow.SaveChangesAsync();
        }

        // Act - Remove deliveryId2, start, and complete
        await using (var context = CreateDbContext())
        {
            var repo = new RouteRepository(context);
            var uow = CreateUnitOfWork(context);

            var route = await repo.GetByIdAsync(routeId);
            Assert.NotNull(route);

            route.RemoveDelivery(deliveryId2);
            var actualStart = DateTimeOffset.UtcNow;
            route.Start(actualStart);
            route.Complete(actualStart.AddHours(2));

            await repo.UpdateAsync(route);
            await uow.SaveChangesAsync();
        }

        // Assert
        await using (var context = CreateDbContext())
        {
            var repo = new RouteRepository(context);
            var retrieved = await repo.GetByIdAsync(routeId);

            Assert.NotNull(retrieved);
            Assert.Equal(RouteStatus.Completed, retrieved.Status);
            Assert.Single(retrieved.DeliveryIds);
            Assert.Equal(deliveryId1, retrieved.DeliveryIds.First());
            Assert.Equal(vehicleId, retrieved.AssignedVehicleId);
            Assert.Equal(driverId, retrieved.AssignedDriverId);
            Assert.NotNull(retrieved.ActualDeparture);
            Assert.NotNull(retrieved.ActualArrival);
        }
    }
}
