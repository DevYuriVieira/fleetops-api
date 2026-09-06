using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Persistence;

public class MaintenancePersistenceTests : BaseIntegrationTest
{
    [Fact]
    public async Task CreateAndCompleteMaintenance_ShouldPersistMoneyValueObjectAndStatus()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var scheduledDate = DateTimeOffset.UtcNow.AddDays(1);

        await using (var context = CreateDbContext())
        {
            var vRepo = new VehicleRepository(context);
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);

            var vehicle = Vehicle.Create(
                vehicleId, LicensePlate.Create("MNT1234"), VehicleType.Truck, "Mercedes", "Actros", 2021, 5000, 25000m);
            await vRepo.AddAsync(vehicle);

            var maintenance = Maintenance.Create(
                maintenanceId,
                vehicleId,
                MaintenanceType.Preventive,
                "Regular engine maintenance",
                scheduledDate);
            await mRepo.AddAsync(maintenance);

            await uow.SaveChangesAsync();
        }

        // Act - Start and Complete maintenance with Money VO
        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var uow = CreateUnitOfWork(context);

            var m = await mRepo.GetByIdAsync(maintenanceId);
            Assert.NotNull(m);

            var startTime = DateTimeOffset.UtcNow;
            m.Start(startTime);
            m.Complete(startTime.AddHours(2), new Money(1250.75m, "USD"));

            await mRepo.UpdateAsync(m);
            await uow.SaveChangesAsync();
        }

        // Assert
        await using (var context = CreateDbContext())
        {
            var mRepo = new MaintenanceRepository(context);
            var retrieved = await mRepo.GetByIdAsync(maintenanceId);

            Assert.NotNull(retrieved);
            Assert.Equal(MaintenanceStatus.Completed, retrieved.Status);
            Assert.NotNull(retrieved.Cost);
            Assert.Equal(1250.75m, retrieved.Cost.Amount);
            Assert.Equal("USD", retrieved.Cost.Currency);
            Assert.NotNull(retrieved.StartedAt);
            Assert.NotNull(retrieved.CompletedAt);
        }
    }

    [Fact]
    public async Task ScheduleMaintenance_WithNonExistentVehicle_ShouldThrowConflictExceptionDueToForeignKey()
    {
        // Arrange
        var nonExistentVehicleId = Guid.NewGuid();
        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            nonExistentVehicleId,
            MaintenanceType.Corrective,
            "Oil leak fix",
            DateTimeOffset.UtcNow.AddDays(1));

        await using var context = CreateDbContext();
        var repo = new MaintenanceRepository(context);
        var uow = CreateUnitOfWork(context);

        await repo.AddAsync(maintenance);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
        Assert.Contains("foreign key constraint", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
