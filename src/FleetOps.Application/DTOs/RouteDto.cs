namespace FleetOps.Application.DTOs;

public sealed record RouteDto(
    Guid Id,
    AddressDto Origin,
    AddressDto Destination,
    string Status,
    DateTimeOffset PlannedDeparture,
    DateTimeOffset? ActualDeparture,
    DateTimeOffset EstimatedArrival,
    DateTimeOffset? ActualArrival,
    Guid? AssignedVehicleId,
    Guid? AssignedDriverId,
    IReadOnlyCollection<Guid> DeliveryIds,
    string? CancellationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
