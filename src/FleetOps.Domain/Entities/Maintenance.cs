namespace FleetOps.Domain.Entities;

using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.Primitives;
using FleetOps.Domain.ValueObjects;

public sealed class Maintenance : AggregateRoot
{
    public Guid VehicleId { get; private set; }
    public MaintenanceType Type { get; private set; }
    public string Description { get; private set; }
    public MaintenanceStatus Status { get; private set; }
    public DateTimeOffset ScheduledAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public Money? Cost { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Maintenance()
    {
        Description = null!;
    }

    private Maintenance(
        Guid id,
        Guid vehicleId,
        MaintenanceType type,
        string description,
        DateTimeOffset scheduledAt,
        DateTimeOffset createdAt) : base(id)
    {
        VehicleId = vehicleId;
        Type = type;
        Description = description;
        ScheduledAt = scheduledAt;
        Status = MaintenanceStatus.Scheduled;
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public static Maintenance Create(
        Guid id,
        Guid vehicleId,
        MaintenanceType type,
        string description,
        DateTimeOffset scheduledAt)
    {
        if (vehicleId == Guid.Empty)
        {
            throw new DomainValidationException("Vehicle identifier cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new DomainValidationException("Maintenance description cannot be empty.");
        }

        return new Maintenance(
            id,
            vehicleId,
            type,
            description.Trim(),
            scheduledAt,
            DateTimeOffset.UtcNow);
    }

    public void Start(DateTimeOffset startedAt)
    {
        if (Status != MaintenanceStatus.Scheduled)
        {
            throw new InvalidMaintenanceStateException("Maintenance can only be started when in Scheduled status.");
        }

        StartedAt = startedAt;
        Status = MaintenanceStatus.InProgress;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new MaintenanceStartedDomainEvent(Id, VehicleId, startedAt, UpdatedAt.Value));
    }

    public void Complete(DateTimeOffset completedAt, Money cost)
    {
        ArgumentNullException.ThrowIfNull(cost);

        if (Status != MaintenanceStatus.InProgress)
        {
            throw new InvalidMaintenanceStateException("Maintenance can only be completed when InProgress.");
        }

        if (StartedAt.HasValue && completedAt < StartedAt.Value)
        {
            throw new DomainValidationException("Completion time cannot be before start time.");
        }

        Cost = cost;
        CompletedAt = completedAt;
        Status = MaintenanceStatus.Completed;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new MaintenanceCompletedDomainEvent(Id, VehicleId, cost, completedAt, UpdatedAt.Value));
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Cancellation reason cannot be empty.");
        }

        if (Status == MaintenanceStatus.Completed)
        {
            throw new InvalidMaintenanceStateException("Cannot cancel an already completed maintenance.");
        }

        if (Status == MaintenanceStatus.Cancelled)
        {
            throw new InvalidMaintenanceStateException("Maintenance is already cancelled.");
        }

        Status = MaintenanceStatus.Cancelled;
        CancellationReason = reason.Trim();
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new MaintenanceCancelledDomainEvent(Id, VehicleId, CancellationReason, UpdatedAt.Value));
    }
}
