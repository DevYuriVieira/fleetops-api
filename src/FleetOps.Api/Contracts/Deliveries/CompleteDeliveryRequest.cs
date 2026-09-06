namespace FleetOps.Api.Contracts.Deliveries;

public sealed record CompleteDeliveryRequest(DateTimeOffset? ActualDeliveryTime = null);
