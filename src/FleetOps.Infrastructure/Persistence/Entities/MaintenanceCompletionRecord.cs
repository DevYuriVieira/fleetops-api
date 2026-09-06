namespace FleetOps.Infrastructure.Persistence.Entities;

public sealed class MaintenanceCompletionRecord
{
    public Guid MessageId { get; private set; }
    public Guid MaintenanceId { get; private set; }
    public Guid VehicleId { get; private set; }
    public DateTimeOffset CompletedOnUtc { get; private set; }
    public DateTimeOffset ProcessedOnUtc { get; private set; }

    private MaintenanceCompletionRecord()
    {
    }

    public MaintenanceCompletionRecord(
        Guid messageId,
        Guid maintenanceId,
        Guid vehicleId,
        DateTimeOffset completedOnUtc,
        DateTimeOffset processedOnUtc)
    {
        MessageId = messageId;
        MaintenanceId = maintenanceId;
        VehicleId = vehicleId;
        CompletedOnUtc = completedOnUtc;
        ProcessedOnUtc = processedOnUtc;
    }
}
