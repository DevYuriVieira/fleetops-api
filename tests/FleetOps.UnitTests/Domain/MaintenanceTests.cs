namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.ValueObjects;

public class MaintenanceTests
{
    private static Maintenance CreateValidMaintenance()
    {
        return Maintenance.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MaintenanceType.Preventive,
            "Oil and filter change",
            DateTimeOffset.UtcNow.AddDays(2));
    }

    [Fact]
    public void Create_ShouldInstantiateMaintenance_WhenValid()
    {
        var id = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var scheduled = DateTimeOffset.UtcNow.AddDays(3);

        var maintenance = Maintenance.Create(
            id,
            vehicleId,
            MaintenanceType.Corrective,
            "Brake pads replacement",
            scheduled);

        Assert.Equal(id, maintenance.Id);
        Assert.Equal(vehicleId, maintenance.VehicleId);
        Assert.Equal(MaintenanceType.Corrective, maintenance.Type);
        Assert.Equal("Brake pads replacement", maintenance.Description);
        Assert.Equal(scheduled, maintenance.ScheduledAt);
        Assert.Equal(MaintenanceStatus.Scheduled, maintenance.Status);
        Assert.Null(maintenance.StartedAt);
        Assert.Null(maintenance.CompletedAt);
        Assert.Null(maintenance.Cost);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_ShouldThrowValidationException_WhenDescriptionIsEmpty(string description)
    {
        Assert.Throws<DomainValidationException>(() => Maintenance.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            MaintenanceType.Inspection,
            description,
            DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Lifecycle_ShouldProgressCorrectly()
    {
        var maintenance = CreateValidMaintenance();
        var startedAt = DateTimeOffset.UtcNow;

        maintenance.Start(startedAt);
        Assert.Equal(MaintenanceStatus.InProgress, maintenance.Status);
        Assert.Equal(startedAt, maintenance.StartedAt);
        Assert.Contains(maintenance.DomainEvents, e => e is MaintenanceStartedDomainEvent);

        maintenance.ClearDomainEvents();
        var completedAt = startedAt.AddHours(3);
        var cost = new Money(350.00m, "USD");
        maintenance.Complete(completedAt, cost);

        Assert.Equal(MaintenanceStatus.Completed, maintenance.Status);
        Assert.Equal(completedAt, maintenance.CompletedAt);
        Assert.Equal(cost, maintenance.Cost);
        Assert.Contains(maintenance.DomainEvents, e => e is MaintenanceCompletedDomainEvent);
    }

    [Fact]
    public void Complete_ShouldThrowValidationException_WhenCompletedAtIsBeforeStartedAt()
    {
        var maintenance = CreateValidMaintenance();
        var startedAt = DateTimeOffset.UtcNow;
        maintenance.Start(startedAt);

        Assert.Throws<DomainValidationException>(() => maintenance.Complete(startedAt.AddHours(-1), new Money(100m, "USD")));
    }

    [Fact]
    public void Complete_ShouldThrowInvalidStateException_WhenNotScheduled()
    {
        var maintenance = CreateValidMaintenance();

        Assert.Throws<InvalidMaintenanceStateException>(() => maintenance.Complete(DateTimeOffset.UtcNow, new Money(100m, "USD")));
    }

    [Fact]
    public void Cancel_ShouldChangeStatusToCancelledAndRaiseEvent()
    {
        var maintenance = CreateValidMaintenance();

        maintenance.Cancel("Vehicle decommissioned");

        Assert.Equal(MaintenanceStatus.Cancelled, maintenance.Status);
        Assert.Equal("Vehicle decommissioned", maintenance.CancellationReason);
        Assert.Contains(maintenance.DomainEvents, e => e is MaintenanceCancelledDomainEvent);
    }

    [Fact]
    public void Cancel_ShouldThrowInvalidStateException_WhenAlreadyCompleted()
    {
        var maintenance = CreateValidMaintenance();
        var startedAt = DateTimeOffset.UtcNow;
        maintenance.Start(startedAt);
        maintenance.Complete(startedAt.AddHours(1), new Money(200m, "USD"));

        Assert.Throws<InvalidMaintenanceStateException>(() => maintenance.Cancel("No cancellation"));
    }
}
