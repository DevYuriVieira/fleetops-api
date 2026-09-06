namespace FleetOps.Api.Contracts.Maintenance;

public sealed record StartMaintenanceRequest(DateTimeOffset? StartedAt = null);
