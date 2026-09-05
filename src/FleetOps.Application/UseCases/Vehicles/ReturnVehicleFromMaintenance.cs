namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record ReturnVehicleFromMaintenanceCommand(Guid VehicleId);

public sealed class ReturnVehicleFromMaintenanceUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ReturnVehicleFromMaintenanceUseCase(
        IVehicleRepository vehicleRepository,
        IMaintenanceRepository maintenanceRepository,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _maintenanceRepository = maintenanceRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(ReturnVehicleFromMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        var activeMaintenance = await _maintenanceRepository.GetActiveByVehicleIdAsync(command.VehicleId, cancellationToken);
        if (activeMaintenance is not null)
        {
            throw new ConflictException(
                $"Vehicle '{command.VehicleId}' cannot be returned directly while active maintenance '{activeMaintenance.Id}' is in status '{activeMaintenance.Status}'. Complete or cancel the maintenance record first.");
        }

        vehicle.ReturnFromMaintenance();

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
