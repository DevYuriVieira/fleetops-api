namespace FleetOps.Api.Extensions;

using FleetOps.Application.UseCases.Deliveries;
using FleetOps.Application.UseCases.Drivers;
using FleetOps.Application.UseCases.Maintenance;
using FleetOps.Application.UseCases.Routes;
using FleetOps.Application.UseCases.Vehicles;
using Microsoft.Extensions.DependencyInjection;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationUseCases(this IServiceCollection services)
    {
        services.AddScoped<RegisterVehicleUseCase>();
        services.AddScoped<ActivateVehicleUseCase>();
        services.AddScoped<DeactivateVehicleUseCase>();
        services.AddScoped<AssignDriverToVehicleUseCase>();
        services.AddScoped<UnassignDriverFromVehicleUseCase>();
        services.AddScoped<UpdateVehicleMileageUseCase>();
        services.AddScoped<SendVehicleToMaintenanceUseCase>();
        services.AddScoped<ReturnVehicleFromMaintenanceUseCase>();

        services.AddScoped<RegisterDriverUseCase>();
        services.AddScoped<ActivateDriverUseCase>();
        services.AddScoped<DeactivateDriverUseCase>();
        services.AddScoped<SuspendDriverUseCase>();

        services.AddScoped<CreateDeliveryUseCase>();
        services.AddScoped<AssignDeliveryUseCase>();
        services.AddScoped<StartDeliveryUseCase>();
        services.AddScoped<CompleteDeliveryUseCase>();
        services.AddScoped<CancelDeliveryUseCase>();

        services.AddScoped<CreateRouteUseCase>();
        services.AddScoped<AssignRouteUseCase>();
        services.AddScoped<AddDeliveryToRouteUseCase>();
        services.AddScoped<RemoveDeliveryFromRouteUseCase>();
        services.AddScoped<StartRouteUseCase>();
        services.AddScoped<CompleteRouteUseCase>();
        services.AddScoped<CancelRouteUseCase>();

        services.AddScoped<ScheduleMaintenanceUseCase>();
        services.AddScoped<StartMaintenanceUseCase>();
        services.AddScoped<CompleteMaintenanceUseCase>();
        services.AddScoped<CancelMaintenanceUseCase>();
        services.AddScoped<ProcessMaintenanceCompletedUseCase>();

        return services;
    }
}
