using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Persistence;

public class VehiclePersistenceTests : BaseIntegrationTest
{
    [Fact]
    public async Task CreateAndRetrieveVehicle_ShouldPersistAllPropertiesCorrectly()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId,
            LicensePlate.Create("ABC1D23"),
            VehicleType.Truck,
            "Volvo",
            "FH16",
            2023,
            15000,
            50000m);

        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);

            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var retrieved = await repo.GetByIdAsync(vehicleId);

            Assert.NotNull(retrieved);
            Assert.Equal(vehicleId, retrieved.Id);
            Assert.Equal("Volvo", retrieved.Make);
            Assert.Equal("FH16", retrieved.Model);
            Assert.Equal(2023, retrieved.Year);
            Assert.Equal("ABC1D23", retrieved.LicensePlate.Value);
            Assert.Equal(50000m, retrieved.CapacityKg);
            Assert.Equal(15000, retrieved.Mileage);
            Assert.Equal(VehicleType.Truck, retrieved.Type);
            Assert.Equal(VehicleStatus.Active, retrieved.Status);
            Assert.Null(retrieved.CurrentDriverId);
        }
    }

    [Fact]
    public async Task UpdateVehicle_ShouldPersistStateChanges()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId,
            LicensePlate.Create("XYZ9W87"),
            VehicleType.Truck,
            "Scania",
            "R500",
            2022,
            20000,
            40000m);

        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var dRepo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);

            var driver = Driver.Create(driverId, "Driver Vehicle", "DL55555555", "dv@test.com", "+1555555555");
            await dRepo.AddAsync(driver);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Act - Update mileage and assign driver
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            var v = await repo.GetByIdAsync(vehicleId);
            Assert.NotNull(v);

            v.AssignDriver(driverId);
            v.UpdateMileage(20500);

            await repo.UpdateAsync(v);
            await uow.SaveChangesAsync();
        }

        // Assert
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var retrieved = await repo.GetByIdAsync(vehicleId);

            Assert.NotNull(retrieved);
            Assert.Equal(driverId, retrieved.CurrentDriverId);
            Assert.Equal(20500, retrieved.Mileage);
            Assert.Equal(VehicleStatus.Active, retrieved.Status);
        }
    }

    [Fact]
    public async Task AddVehicle_WithDuplicateLicensePlate_ShouldThrowConflictException()
    {
        // Arrange
        var plate = LicensePlate.Create("DUP1234");
        var vehicle1 = Vehicle.Create(Guid.NewGuid(), plate, VehicleType.Van, "Make1", "Model1", 2021, 100, 1000m);
        var vehicle2 = Vehicle.Create(Guid.NewGuid(), plate, VehicleType.Van, "Make2", "Model2", 2022, 200, 2000m);

        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle1);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle2);

            var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
            Assert.Contains("license plate already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ConcurrentVehicleUpdate_ShouldTriggerOptimisticConcurrencyConflict()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("CNC1234"), VehicleType.Van, "Ford", "Transit", 2020, 10000, 1500m);

        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Simulate two concurrent contexts loading the same vehicle
        await using var contextA = CreateDbContext();
        await using var contextB = CreateDbContext();

        var repoA = new VehicleRepository(contextA);
        var uowA = CreateUnitOfWork(contextA);
        var vehicleA = await repoA.GetByIdAsync(vehicleId);

        var repoB = new VehicleRepository(contextB);
        var uowB = CreateUnitOfWork(contextB);
        var vehicleB = await repoB.GetByIdAsync(vehicleId);

        Assert.NotNull(vehicleA);
        Assert.NotNull(vehicleB);

        // Context A updates and saves first
        vehicleA.UpdateMileage(10500);
        await repoA.UpdateAsync(vehicleA);
        await uowA.SaveChangesAsync();

        // Context B tries to update the stale vehicle
        vehicleB.UpdateMileage(11000);
        await repoB.UpdateAsync(vehicleB);

        // Act & Assert: Must throw ConflictException due to xmin mismatch
        var ex = await Assert.ThrowsAsync<ConflictException>(() => uowB.SaveChangesAsync());
        Assert.Contains("modified or deleted by another concurrent transaction", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
