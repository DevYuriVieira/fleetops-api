namespace FleetOps.Infrastructure.Services;

using System.Text.Json;
using FleetOps.Domain.Events;

public static class OutboxEventRegistry
{
    private static readonly Dictionary<string, Type> NameToType = new(StringComparer.OrdinalIgnoreCase)
    {
        // Vehicle Events
        ["VehicleActivated"] = typeof(VehicleActivatedDomainEvent),
        ["VehicleDeactivated"] = typeof(VehicleDeactivatedDomainEvent),
        ["DriverAssignedToVehicle"] = typeof(DriverAssignedToVehicleDomainEvent),
        ["DriverUnassignedFromVehicle"] = typeof(DriverUnassignedFromVehicleDomainEvent),
        ["VehicleMileageUpdated"] = typeof(VehicleMileageUpdatedDomainEvent),
        ["VehicleSentToMaintenance"] = typeof(VehicleSentToMaintenanceDomainEvent),
        ["VehicleReturnedFromMaintenance"] = typeof(VehicleReturnedFromMaintenanceDomainEvent),

        // Driver Events
        ["DriverActivated"] = typeof(DriverActivatedDomainEvent),
        ["DriverSuspended"] = typeof(DriverSuspendedDomainEvent),
        ["DriverDeactivated"] = typeof(DriverDeactivatedDomainEvent),

        // Delivery Events
        ["DeliveryAssigned"] = typeof(DeliveryAssignedDomainEvent),
        ["DeliveryStarted"] = typeof(DeliveryStartedDomainEvent),
        ["DeliveryCompleted"] = typeof(DeliveryCompletedDomainEvent),
        ["DeliveryCancelled"] = typeof(DeliveryCancelledDomainEvent),

        // Route Events
        ["RouteStarted"] = typeof(RouteStartedDomainEvent),
        ["RouteCompleted"] = typeof(RouteCompletedDomainEvent),
        ["RouteCancelled"] = typeof(RouteCancelledDomainEvent),
        ["DeliveryAddedToRoute"] = typeof(DeliveryAddedToRouteDomainEvent),
        ["DeliveryRemovedFromRoute"] = typeof(DeliveryRemovedFromRouteDomainEvent),

        // Maintenance Events
        ["MaintenanceStarted"] = typeof(MaintenanceStartedDomainEvent),
        ["MaintenanceCompleted"] = typeof(MaintenanceCompletedDomainEvent),
        ["MaintenanceCancelled"] = typeof(MaintenanceCancelledDomainEvent)
    };

    private static readonly Dictionary<Type, string> TypeToName =
        NameToType.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    public static string GetEventName(Type eventType)
    {
        ArgumentNullException.ThrowIfNull(eventType);

        if (TypeToName.TryGetValue(eventType, out var name))
        {
            return name;
        }

        return eventType.Name;
    }

    public static Type? GetEventType(string eventName)
    {
        if (string.IsNullOrWhiteSpace(eventName))
        {
            return null;
        }

        return NameToType.GetValueOrDefault(eventName);
    }

    public static IDomainEvent? Deserialize(string eventName, string payload, JsonSerializerOptions options)
    {
        var type = GetEventType(eventName);
        if (type is null)
        {
            return null;
        }

        return JsonSerializer.Deserialize(payload, type, options) as IDomainEvent;
    }
}
