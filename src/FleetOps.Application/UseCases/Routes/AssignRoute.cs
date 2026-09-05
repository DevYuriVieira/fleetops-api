namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record AssignRouteCommand(Guid RouteId, Guid VehicleId, Guid DriverId);

public sealed class AssignRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignRouteUseCase(
        IRouteRepository routeRepository,
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(AssignRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken)
            ?? throw new NotFoundException("Route", command.RouteId);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        if (vehicle.Status != VehicleStatus.Active)
        {
            throw new ConflictException($"Vehicle '{vehicle.Id}' cannot be assigned because vehicle status is {vehicle.Status}.");
        }

        var driver = await _driverRepository.GetByIdAsync(command.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", command.DriverId);

        if (driver.Status != DriverStatus.Active)
        {
            throw new ConflictException($"Driver '{driver.Id}' cannot be assigned because driver status is {driver.Status}.");
        }

        route.Assign(command.VehicleId, command.DriverId);

        await _routeRepository.UpdateAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
