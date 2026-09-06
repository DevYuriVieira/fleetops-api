using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.IntegrationTests.Common;
using Xunit;

namespace FleetOps.IntegrationTests.Cancellation;

public class CancellationTests : BaseIntegrationTest
{
    [Fact]
    public async Task SaveChangesAsync_WithCancelledToken_ShouldThrowOperationCanceledExceptionAndPersistNothing()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("CNC9999"), VehicleType.Truck, "Iveco", "Stralis", 2020, 1000, 20000m);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately

        await using var context = CreateDbContext();
        var repo = new VehicleRepository(context);
        var uow = CreateUnitOfWork(context);

        await repo.AddAsync(vehicle, CancellationToken.None);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => uow.SaveChangesAsync(cts.Token));

        // Verify nothing was persisted
        await using var verifyContext = CreateDbContext();
        var v = await verifyContext.Vehicles.FindAsync(vehicleId);
        Assert.Null(v);
    }

    [Fact]
    public async Task GetByIdAsync_WithCancelledToken_ShouldThrowOperationCanceledException()
    {
        // Arrange
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await using var context = CreateDbContext();
        var repo = new VehicleRepository(context);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repo.GetByIdAsync(Guid.NewGuid(), cts.Token));
    }
}
