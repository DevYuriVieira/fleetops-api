namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record ProcessMaintenanceCompletedCommand(Guid MaintenanceId, Guid VehicleId);

public sealed class ProcessMaintenanceCompletedUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessMaintenanceCompletedUseCase(
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task ExecuteAsync(ProcessMaintenanceCompletedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        if (vehicle.Status == VehicleStatus.UnderMaintenance)
        {
            vehicle.ReturnFromMaintenance();
            await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
