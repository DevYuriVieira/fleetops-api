namespace FleetOps.Application.DTOs;

public sealed record MaintenanceDto(
    Guid Id,
    Guid VehicleId,
    string Type,
    string Description,
    string Status,
    DateTimeOffset ScheduledAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    decimal? CostAmount,
    string? CostCurrency,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
