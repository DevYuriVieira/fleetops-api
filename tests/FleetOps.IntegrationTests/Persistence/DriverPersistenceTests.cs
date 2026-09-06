using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Persistence;

public class DriverPersistenceTests : BaseIntegrationTest
{
    [Fact]
    public async Task CreateAndRetrieveDriver_ShouldPersistAllPropertiesCorrectly()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var driver = Driver.Create(
            driverId,
            "John Doe",
            "DL12345678",
            "john.doe@example.com",
            "+1234567890");

        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);

            await repo.AddAsync(driver);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var retrieved = await repo.GetByIdAsync(driverId);

            Assert.NotNull(retrieved);
            Assert.Equal(driverId, retrieved.Id);
            Assert.Equal("John Doe", retrieved.FullName);
            Assert.Equal("DL12345678", retrieved.LicenseNumber);
            Assert.Equal("john.doe@example.com", retrieved.Email);
            Assert.Equal("+1234567890", retrieved.PhoneNumber);
            Assert.Equal(DriverStatus.Active, retrieved.Status);
        }
    }

    [Fact]
    public async Task UpdateDriver_ShouldPersistStateChanges()
    {
        // Arrange
        var driverId = Guid.NewGuid();
        var driver = Driver.Create(
            driverId,
            "Jane Smith",
            "DL87654321",
            "jane.smith@example.com",
            "+1987654321");

        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(driver);
            await uow.SaveChangesAsync();
        }

        // Act - Suspend driver
        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);
            var d = await repo.GetByIdAsync(driverId);
            Assert.NotNull(d);

            d.Suspend("License pending verification");

            await repo.UpdateAsync(d);
            await uow.SaveChangesAsync();
        }

        // Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var retrieved = await repo.GetByIdAsync(driverId);

            Assert.NotNull(retrieved);
            Assert.Equal(DriverStatus.Suspended, retrieved.Status);
        }
    }

    [Fact]
    public async Task AddDriver_WithDuplicateLicenseNumber_ShouldThrowConflictException()
    {
        // Arrange
        var licenseNumber = "DL99999999";
        var driver1 = Driver.Create(Guid.NewGuid(), "Driver One", licenseNumber, "d1@test.com", "+1111111111");
        var driver2 = Driver.Create(Guid.NewGuid(), "Driver Two", licenseNumber, "d2@test.com", "+2222222222");

        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(driver1);
            await uow.SaveChangesAsync();
        }

        // Act & Assert
        await using (var context = CreateDbContext())
        {
            var repo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(driver2);

            var ex = await Assert.ThrowsAsync<ConflictException>(() => uow.SaveChangesAsync());
            Assert.Contains("license number already exists", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
