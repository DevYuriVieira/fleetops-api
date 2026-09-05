namespace FleetOps.Application.DTOs;

using FleetOps.Domain.Entities;
using FleetOps.Domain.ValueObjects;

public static class MappingExtensions
{
    public static VehicleDto ToDto(this Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);

        return new VehicleDto(
            vehicle.Id,
            vehicle.LicensePlate.Value,
            vehicle.Type.ToString(),
            vehicle.Status.ToString(),
            vehicle.Make,
            vehicle.Model,
            vehicle.Year,
            vehicle.Mileage,
            vehicle.CapacityKg,
            vehicle.CurrentDriverId,
            vehicle.CreatedAt,
            vehicle.UpdatedAt);
    }

    public static DriverDto ToDto(this Driver driver)
    {
        ArgumentNullException.ThrowIfNull(driver);

        return new DriverDto(
            driver.Id,
            driver.FullName,
            driver.LicenseNumber,
            driver.Email,
            driver.PhoneNumber,
            driver.Status.ToString(),
            driver.CreatedAt,
            driver.UpdatedAt);
    }

    public static AddressDto ToDto(this Address address)
    {
        ArgumentNullException.ThrowIfNull(address);

        return new AddressDto(
            address.Street,
            address.Number,
            address.Neighborhood,
            address.City,
            address.State,
            address.PostalCode,
            address.Country,
            address.Complement);
    }

    public static Address ToDomain(this AddressDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new Address(
            dto.Street,
            dto.Number,
            dto.Neighborhood,
            dto.City,
            dto.State,
            dto.PostalCode,
            dto.Country,
            dto.Complement);
    }

    public static DeliveryDto ToDto(this Delivery delivery)
    {
        ArgumentNullException.ThrowIfNull(delivery);

        return new DeliveryDto(
            delivery.Id,
            delivery.TrackingCode.Value,
            delivery.Origin.ToDto(),
            delivery.Destination.ToDto(),
            delivery.Status.ToString(),
            delivery.Priority.ToString(),
            delivery.WeightKg,
            delivery.AssignedVehicleId,
            delivery.AssignedDriverId,
            delivery.EstimatedDeliveryTime,
            delivery.ActualDeliveryTime,
            delivery.CancellationReason,
            delivery.CreatedAt,
            delivery.UpdatedAt);
    }

    public static RouteDto ToDto(this Route route)
    {
        ArgumentNullException.ThrowIfNull(route);

        return new RouteDto(
            route.Id,
            route.Origin.ToDto(),
            route.Destination.ToDto(),
            route.Status.ToString(),
            route.PlannedDeparture,
            route.ActualDeparture,
            route.EstimatedArrival,
            route.ActualArrival,
            route.AssignedVehicleId,
            route.AssignedDriverId,
            route.DeliveryIds,
            route.CancellationReason,
            route.CreatedAt,
            route.UpdatedAt);
    }

    public static MaintenanceDto ToDto(this Maintenance maintenance)
    {
        ArgumentNullException.ThrowIfNull(maintenance);

        return new MaintenanceDto(
            maintenance.Id,
            maintenance.VehicleId,
            maintenance.Type.ToString(),
            maintenance.Description,
            maintenance.Status.ToString(),
            maintenance.ScheduledAt,
            maintenance.StartedAt,
            maintenance.CompletedAt,
            maintenance.Cost?.Amount,
            maintenance.Cost?.Currency,
            maintenance.CancellationReason,
            maintenance.CreatedAt,
            maintenance.UpdatedAt);
    }
}
