namespace FleetOps.Application.DTOs;

public sealed record VehicleDto(
    Guid Id,
    string LicensePlate,
    string Type,
    string Status,
    string Make,
    string Model,
    int Year,
    int Mileage,
    decimal CapacityKg,
    Guid? CurrentDriverId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
