namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;

public sealed record ScheduleMaintenanceCommand(
    Guid VehicleId,
    string Type,
    string Description,
    DateTimeOffset ScheduledAt);

public sealed class ScheduleMaintenanceUseCase
{
    private readonly IMaintenanceRepository _maintenanceRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ScheduleMaintenanceUseCase(
        IMaintenanceRepository maintenanceRepository,
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork)
    {
        _maintenanceRepository = maintenanceRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<MaintenanceDto> ExecuteAsync(ScheduleMaintenanceCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        var activeMaintenance = await _maintenanceRepository.GetActiveByVehicleIdAsync(command.VehicleId, cancellationToken);
        if (activeMaintenance is not null)
        {
            throw new ConflictException($"Vehicle '{vehicle.Id}' already has an active maintenance record '{activeMaintenance.Id}' with status '{activeMaintenance.Status}'.");
        }

        if (!Enum.TryParse<MaintenanceType>(command.Type, true, out var maintenanceType))
        {
            throw new ValidationException($"Invalid maintenance type: '{command.Type}'.");
        }

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            maintenanceType,
            command.Description,
            command.ScheduledAt);

        await _maintenanceRepository.AddAsync(maintenance, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return maintenance.ToDto();
    }
}
