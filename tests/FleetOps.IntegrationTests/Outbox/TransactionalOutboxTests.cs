using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace FleetOps.IntegrationTests.Outbox;

public class TransactionalOutboxTests : BaseIntegrationTest
{
    [Fact]
    public async Task SaveChangesAsync_ShouldPersistAggregateAndOutboxMessageAtomically()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("BOX1234"), VehicleType.Van, "Iveco", "Daily", 2023, 100, 3500m);

        // Raise a domain event by updating mileage
        vehicle.UpdateMileage(200);
        Assert.Single(vehicle.DomainEvents);

        // Act
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Assert: In-memory events cleared after successful save
        Assert.Empty(vehicle.DomainEvents);

        // Assert: Database has both vehicle and outbox message
        await using (var context = CreateDbContext())
        {
            var v = await context.Vehicles.FindAsync(vehicleId);
            Assert.NotNull(v);

            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "VehicleMileageUpdated" && m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(outboxMessage);
            Assert.Equal("VehicleMileageUpdated", outboxMessage.EventType);
            Assert.Contains(vehicleId.ToString(), outboxMessage.Payload);
            Assert.Null(outboxMessage.ProcessedOnUtc);
            Assert.Equal(0, outboxMessage.Attempts);
        }
    }

    [Fact]
    public async Task SaveChangesAsync_WhenPersistenceFails_PreservesInMemoryDomainEventsAndRollsBackOutbox()
    {
        // Arrange
        var plate = LicensePlate.Create("BOX9999");
        var vehicle1 = Vehicle.Create(
            Guid.NewGuid(), plate, VehicleType.Van, "Brand1", "Model1", 2021, 0, 1000m);

        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle1);
            await uow.SaveChangesAsync();
        }

        // Clear outbox from first save
        await using (var context = CreateDbContext())
        {
            await context.Database.ExecuteSqlRawAsync("TRUNCATE TABLE outbox_messages CASCADE;");
        }

        // Create vehicle2 with the same plate to induce a unique constraint violation
        var vehicle2Id = Guid.NewGuid();
        var vehicle2 = Vehicle.Create(
            vehicle2Id, plate, VehicleType.Van, "Brand2", "Model2", 2022, 0, 2000m);

        // Raise domain event
        vehicle2.UpdateMileage(50);
        Assert.Single(vehicle2.DomainEvents);

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle2);

            await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
        }

        // CRITICAL CHECK: In-memory events must NOT be cleared when persistence fails!
        Assert.Single(vehicle2.DomainEvents);

        // CRITICAL CHECK: Database must not contain vehicle2 or its outbox message
        await using (var context = CreateDbContext())
        {
            var v2 = await context.Vehicles.FindAsync(vehicle2Id);
            Assert.Null(v2);

            var outboxMessages = await context.OutboxMessages
                .Where(m => m.Payload.Contains(vehicle2Id.ToString()))
                .ToListAsync();
            Assert.Empty(outboxMessages);
        }
    }
}
