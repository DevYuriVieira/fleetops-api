namespace FleetOps.Api.Contracts.Routes;

public sealed record AssignRouteRequest(Guid VehicleId, Guid DriverId);
