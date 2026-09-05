namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record AssignDriverToVehicleCommand(Guid VehicleId, Guid DriverId);

public sealed class AssignDriverToVehicleUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignDriverToVehicleUseCase(
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(AssignDriverToVehicleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        var driver = await _driverRepository.GetByIdAsync(command.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", command.DriverId);

        if (driver.Status != DriverStatus.Active)
        {
            throw new ConflictException($"Driver '{driver.Id}' cannot be assigned because driver status is {driver.Status}.");
        }

        vehicle.AssignDriver(driver.Id);

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
