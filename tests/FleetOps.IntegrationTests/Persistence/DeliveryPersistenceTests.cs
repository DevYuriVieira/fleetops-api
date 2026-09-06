using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Persistence;

public class DeliveryPersistenceTests : BaseIntegrationTest
{
    [Fact]
    public async Task CreateAndRetrieveDelivery_ShouldPersistTrackingCodeAndAddressValueObjects()
    {
        // Arrange
        var deliveryId = Guid.NewGuid();
        var trackingCode = TrackingCode.Create("TRK-12345678");
        var origin = new Address("Main St", "100", "Downtown", "Springfield", "IL", "62701", "USA");
        var destination = new Address("Elm St", "200", "Uptown", "Chicago", "IL", "60601", "USA");

        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var uow = CreateUnitOfWork(context);

            var delivery = Delivery.Create(
                deliveryId,
                trackingCode,
                origin,
                destination,
                DeliveryPriority.Standard,
                25.5m);

            await repo.AddAsync(delivery);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var retrieved = await repo.GetByIdAsync(deliveryId);

            Assert.NotNull(retrieved);
            Assert.Equal(deliveryId, retrieved.Id);
            Assert.Equal("TRK-12345678", retrieved.TrackingCode.Value);
            Assert.Equal(25.5m, retrieved.WeightKg);
            Assert.Equal(DeliveryStatus.Pending, retrieved.Status);
            Assert.Equal(DeliveryPriority.Standard, retrieved.Priority);

            // Origin Address
            Assert.Equal("Main St", retrieved.Origin.Street);
            Assert.Equal("100", retrieved.Origin.Number);
            Assert.Equal("Downtown", retrieved.Origin.Neighborhood);
            Assert.Equal("Springfield", retrieved.Origin.City);
            Assert.Equal("IL", retrieved.Origin.State);
            Assert.Equal("62701", retrieved.Origin.PostalCode);
            Assert.Equal("USA", retrieved.Origin.Country);

            // Destination Address
            Assert.Equal("Elm St", retrieved.Destination.Street);
            Assert.Equal("200", retrieved.Destination.Number);
            Assert.Equal("Uptown", retrieved.Destination.Neighborhood);
            Assert.Equal("Chicago", retrieved.Destination.City);
            Assert.Equal("IL", retrieved.Destination.State);
            Assert.Equal("60601", retrieved.Destination.PostalCode);
            Assert.Equal("USA", retrieved.Destination.Country);
        }
    }

    [Fact]
    public async Task UpdateDelivery_ShouldPersistStatusTransitions()
    {
        // Arrange
        var deliveryId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var trackingCode = TrackingCode.Create("TRK-87654321");
        var origin = new Address("Origin St", "10", "Center", "City", "ST", "12345", "USA");
        var destination = new Address("Dest St", "20", "North", "City", "ST", "12345", "USA");

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var dRepo = new DriverRepository(context);
            var repo = new DeliveryRepository(context);
            var uow = CreateUnitOfWork(context);

            var vehicle = Vehicle.Create(
                vehicleId, LicensePlate.Create("DEL0001"), VehicleType.Truck, "Make", "Model", 2022, 1000, 10000m);
            var driver = Driver.Create(
                driverId, "Driver Del", "DL88888888", "del@test.com", "+123456789");
            var delivery = Delivery.Create(deliveryId, trackingCode, origin, destination, DeliveryPriority.Urgent, 10m);

            await vRepo.AddAsync(vehicle);
            await dRepo.AddAsync(driver);
            await repo.AddAsync(delivery);
            await uow.SaveChangesAsync();
        }

        // Act - Assign, Start (Transit), and Deliver
        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var uow = CreateUnitOfWork(context);

            var d = await repo.GetByIdAsync(deliveryId);
            Assert.NotNull(d);

            d.Assign(vehicleId, driverId, DateTimeOffset.UtcNow.AddHours(2));
            d.Start();
            d.Complete(DateTimeOffset.UtcNow.AddHours(1));

            await repo.UpdateAsync(d);
            await uow.SaveChangesAsync();
        }

        // Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var retrieved = await repo.GetByIdAsync(deliveryId);

            Assert.NotNull(retrieved);
            Assert.Equal(DeliveryStatus.Delivered, retrieved.Status);
            Assert.Equal(vehicleId, retrieved.AssignedVehicleId);
            Assert.Equal(driverId, retrieved.AssignedDriverId);
            Assert.NotNull(retrieved.ActualDeliveryTime);
        }
    }

    [Fact]
    public async Task AddDelivery_WithDuplicateTrackingCode_ShouldThrowConflictException()
    {
        // Arrange
        var code = TrackingCode.Create("TRK-DUPLICATE");
        var addr1 = new Address("Street 1", "1", "Hood 1", "City", "ST", "00000", "USA");
        var addr2 = new Address("Street 2", "2", "Hood 2", "City", "ST", "00000", "USA");

        var d1 = Delivery.Create(Guid.NewGuid(), code, addr1, addr2, DeliveryPriority.Low, 5m);
        var d2 = Delivery.Create(Guid.NewGuid(), code, addr1, addr2, DeliveryPriority.Low, 10m);

        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(d1);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DeliveryRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(d2);

            var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
            Assert.Contains("tracking code already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
