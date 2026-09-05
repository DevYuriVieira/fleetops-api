namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record UpdateVehicleMileageCommand(Guid VehicleId, int Mileage);

public sealed class UpdateVehicleMileageUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVehicleMileageUseCase(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(UpdateVehicleMileageCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        vehicle.UpdateMileage(command.Mileage);

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
