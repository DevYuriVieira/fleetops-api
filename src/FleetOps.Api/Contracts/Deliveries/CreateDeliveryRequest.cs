namespace FleetOps.Api.Contracts.Deliveries;

using FleetOps.Application.DTOs;

public sealed record CreateDeliveryRequest(
    string TrackingCode,
    AddressDto Origin,
    AddressDto Destination,
    string Priority,
    decimal WeightKg);
