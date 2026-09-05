namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record StartMaintenanceCommand(Guid MaintenanceId, DateTimeOffset? StartedAt = null);

public sealed class StartMaintenanceUseCase
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartMaintenanceUseCase(
        IMaintenanceRepository maintenanceRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceDto> ExecuteAsync(StartMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var maintenance = await _maintenanceRepository.GetByIdAsync(command.MaintenanceId, cancellationToken)
            ?? throw new NotFoundException("Maintenance", command.MaintenanceId);

        var vehicle = await _vehicleRepository.GetByIdAsync(maintenance.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", maintenance.VehicleId);

        if (vehicle.Status != VehicleStatus.UnderMaintenance)
        {
            vehicle.SendToMaintenance();
            await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        }

        var startTime = command.StartedAt ?? DateTimeOffset.UtcNow;
        maintenance.Start(startTime);

        await _maintenanceRepository.UpdateAsync(maintenance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return maintenance.ToDto();
    }
}
