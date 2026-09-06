namespace FleetOps.Api.Contracts.Routes;

public sealed record CompleteRouteRequest(DateTimeOffset? ActualArrival = null);
