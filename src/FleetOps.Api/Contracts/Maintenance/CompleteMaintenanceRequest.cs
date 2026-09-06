namespace FleetOps.Api.Contracts.Maintenance;

public sealed record CompleteMaintenanceRequest(
    decimal CostAmount,
    string CostCurrency = "USD",
    DateTimeOffset? CompletedAt = null,
    bool ReturnVehicleToActive = true);
