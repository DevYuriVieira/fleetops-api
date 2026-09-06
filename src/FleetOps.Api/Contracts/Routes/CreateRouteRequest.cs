namespace FleetOps.Api.Contracts.Routes;

using FleetOps.Application.DTOs;

public sealed record CreateRouteRequest(
    AddressDto Origin,
    AddressDto Destination,
    DateTimeOffset PlannedDeparture,
    DateTimeOffset EstimatedArrival);
