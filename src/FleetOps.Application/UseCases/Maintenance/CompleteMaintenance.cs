namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;

public sealed record CompleteMaintenanceCommand(
    Guid MaintenanceId,
    decimal CostAmount,
    string CostCurrency = "USD",
    DateTimeOffset? CompletedAt = null,
    bool ReturnVehicleToActive = true);

public sealed class CompleteMaintenanceUseCase
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteMaintenanceUseCase(
        IMaintenanceRepository maintenanceRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceDto> ExecuteAsync(CompleteMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var maintenance = await _maintenanceRepository.GetByIdAsync(command.MaintenanceId, cancellationToken)
            ?? throw new NotFoundException("Maintenance", command.MaintenanceId);

        var vehicle = await _vehicleRepository.GetByIdAsync(maintenance.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", maintenance.VehicleId);

        var cost = new Money(command.CostAmount, command.CostCurrency);
        var completionTime = command.CompletedAt ?? DateTimeOffset.UtcNow;

        maintenance.Complete(completionTime, cost);

        if (command.ReturnVehicleToActive && vehicle.Status == VehicleStatus.UnderMaintenance)
        {
            vehicle.ReturnFromMaintenance();
            await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        }

        await _maintenanceRepository.UpdateAsync(maintenance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return maintenance.ToDto();
    }
}
