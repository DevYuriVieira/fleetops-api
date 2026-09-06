using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Concurrency;

public class VehicleMaintenanceConcurrencyTests : BaseIntegrationTest
{
    [Fact]
    public async Task SecondActiveMaintenanceForSameVehicle_MustBeRejectedByDatabasePartialUniqueIndex()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("MNT0001"), VehicleType.Truck, "Scania", "G450", 2022, 1000, 30000m);

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await vRepo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Schedule first maintenance (Scheduled status)
        var m1 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Preventive, "First active maintenance", DateTimeOffset.UtcNow.AddDays(1));

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m1);
            await uow.SaveChangesAsync();
        }

        // Act & Assert: Attempt to schedule second active maintenance (Scheduled status) for the same vehicle
        var m2 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Corrective, "Conflicting active maintenance", DateTimeOffset.UtcNow.AddDays(2));

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m2);

            var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
            Assert.Contains("active maintenance", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task InProgressMaintenance_AlsoConflictsWithScheduledMaintenance_ViaPartialUniqueIndex()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("MNT0002"), VehicleType.Truck, "MAN", "TGX", 2021, 5000, 28000m);

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await vRepo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // First maintenance in Scheduled status
        var m1 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Preventive, "Scheduled maintenance", DateTimeOffset.UtcNow.AddDays(1));

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m1);
            await uow.SaveChangesAsync();
        }

        // Second maintenance in InProgress status
        var m2 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Corrective, "In-progress maintenance", DateTimeOffset.UtcNow);
        m2.Start(DateTimeOffset.UtcNow);

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m2);

            var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
            Assert.Contains("active maintenance", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task NewMaintenance_AllowedAfterPreviousMaintenanceCompletedOrCancelled()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("MNT0003"), VehicleType.Truck, "Volvo", "FMX", 2020, 8000, 32000m);

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await vRepo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        var m1 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Preventive, "Maintenance to complete", DateTimeOffset.UtcNow.AddDays(1));

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m1);
            await uow.SaveChangesAsync();

            // Complete m1
            var start = DateTimeOffset.UtcNow;
            m1.Start(start);
            m1.Complete(start.AddHours(1), new Money(300m, "USD"));
            await mRepo.UpdateAsync(m1);
            await uow.SaveChangesAsync();
        }

        // Act & Assert: Now schedule a second maintenance for the same vehicle
        var m2 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Corrective, "New maintenance after completion", DateTimeOffset.UtcNow.AddDays(3));

        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);
            await mRepo.AddAsync(m2);

            // Must succeed without throwing
            await uow.SaveChangesAsync();

            var retrieved = await mRepo.GetByIdAsync(m2.Id);
            Assert.NotNull(retrieved);
            Assert.Equal(MaintenanceStatus.Scheduled, retrieved.Status);
        }
    }

    [Fact]
    public async Task ConcurrentMaintenanceCreation_ExactlyOneSucceeds_AndOtherFailsWithConflictException()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("MNT0004"), VehicleType.Truck, "DAF", "XF", 2023, 2000, 35000m);

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await vRepo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        var m1 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Preventive, "Concurrent maintenance 1", DateTimeOffset.UtcNow.AddDays(1));
        var m2 = Maintenance.Create(
            Guid.NewGuid(), vehicleId, MaintenanceType.Corrective, "Concurrent maintenance 2", DateTimeOffset.UtcNow.AddDays(2));

        // Act: Run two concurrent requests attempting to insert active maintenances for the same vehicle
        var task1 = Task.Run(async () =>
        {
            await using var ctx = CreateDbContext();
            var repo = new MaintenanceRepository(ctx);
            var uow = CreateUnitOfWork(ctx);
            await repo.AddAsync(m1);
            await uow.SaveChangesAsync();
        });

        var task2 = Task.Run(async () =>
        {
            await using var ctx = CreateDbContext();
            var repo = new MaintenanceRepository(ctx);
            var uow = CreateUnitOfWork(ctx);
            await repo.AddAsync(m2);
            await uow.SaveChangesAsync();
        });

        var results = await Task.WhenAll(
            task1.ContinueWith(t => t.Exception),
            task2.ContinueWith(t => t.Exception));

        var exceptions = results.Where(e => e != null).ToList();

        // Assert: Exactly one operation must succeed, and the other must fail with ConflictException
        Assert.Single(exceptions);
        var baseException = exceptions[0]!.GetBaseException();
        Assert.IsType<ConflictException>(baseException);
        Assert.Contains("active maintenance", baseException.Message, StringComparison.OrdinalIgnoreCase);
    }
}
