namespace FleetOps.Application.DTOs;

public sealed record DriverDto(
    Guid Id,
    string FullName,
    string LicenseNumber,
    string Email,
    string PhoneNumber,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
