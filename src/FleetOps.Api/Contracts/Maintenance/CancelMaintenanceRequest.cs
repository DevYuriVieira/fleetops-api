namespace FleetOps.Api.Contracts.Maintenance;

public sealed record CancelMaintenanceRequest(
    string Reason,
    bool ReturnVehicleToActive = false);
