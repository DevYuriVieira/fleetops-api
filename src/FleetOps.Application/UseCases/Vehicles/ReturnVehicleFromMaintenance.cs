namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record ReturnVehicleFromMaintenanceCommand(Guid VehicleId);

public sealed class ReturnVehicleFromMaintenanceUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnVehicleFromMaintenanceUseCase(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(ReturnVehicleFromMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        vehicle.ReturnFromMaintenance();

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
