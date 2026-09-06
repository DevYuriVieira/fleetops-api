namespace FleetOps.Api.Contracts.Routes;

public sealed record StartRouteRequest(DateTimeOffset? ActualDeparture = null);
