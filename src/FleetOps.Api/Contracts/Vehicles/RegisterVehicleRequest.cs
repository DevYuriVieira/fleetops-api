namespace FleetOps.Api.Contracts.Vehicles;

public sealed record RegisterVehicleRequest(
    string LicensePlate,
    string Type,
    string Make,
    string Model,
    int Year,
    int Mileage,
    decimal CapacityKg);
