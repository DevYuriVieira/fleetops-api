namespace FleetOps.Api.Contracts.Maintenance;

public sealed record ScheduleMaintenanceRequest(
    Guid VehicleId,
    string Type,
    string Description,
    DateTimeOffset ScheduledAt);
