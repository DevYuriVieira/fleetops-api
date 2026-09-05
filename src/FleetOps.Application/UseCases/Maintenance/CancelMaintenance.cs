namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record CancelMaintenanceCommand(
    Guid MaintenanceId,
    string Reason,
    bool ReturnVehicleToActive = false);

public sealed class CancelMaintenanceUseCase
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CancelMaintenanceUseCase(
        IMaintenanceRepository maintenanceRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceDto> ExecuteAsync(CancelMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var maintenance = await _maintenanceRepository.GetByIdAsync(command.MaintenanceId, cancellationToken)
            ?? throw new NotFoundException("Maintenance", command.MaintenanceId);

        maintenance.Cancel(command.Reason);

        if (command.ReturnVehicleToActive)
        {
            var vehicle = await _vehicleRepository.GetByIdAsync(maintenance.VehicleId, cancellationToken);
            if (vehicle is not null && vehicle.Status == VehicleStatus.UnderMaintenance)
            {
                vehicle.ReturnFromMaintenance();
                await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
            }
        }

        await _maintenanceRepository.UpdateAsync(maintenance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return maintenance.ToDto();
    }
}
