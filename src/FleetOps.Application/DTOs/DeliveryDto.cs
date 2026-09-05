namespace FleetOps.Application.DTOs;

public sealed record DeliveryDto(
    Guid Id,
    string TrackingCode,
    AddressDto Origin,
    AddressDto Destination,
    string Status,
    string Priority,
    decimal WeightKg,
    Guid? AssignedVehicleId,
    Guid? AssignedDriverId,
    DateTimeOffset? EstimatedDeliveryTime,
    DateTimeOffset? ActualDeliveryTime,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
