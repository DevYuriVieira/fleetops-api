namespace FleetOps.Api.Contracts.Deliveries;

public sealed record AssignDeliveryRequest(
    Guid VehicleId,
    Guid DriverId,
    DateTimeOffset EstimatedDeliveryTime);
