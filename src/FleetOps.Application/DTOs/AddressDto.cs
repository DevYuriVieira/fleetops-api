namespace FleetOps.Application.DTOs;

public sealed record AddressDto(
    string Street,
    string Number,
    string Neighborhood,
    string City,
    string State,
    string PostalCode,
    string Country,
    string? Complement = null);
