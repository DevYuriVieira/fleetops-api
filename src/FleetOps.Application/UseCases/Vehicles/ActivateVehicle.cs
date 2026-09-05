namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record ActivateVehicleCommand(Guid Id);

public sealed class ActivateVehicleUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateVehicleUseCase(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(ActivateVehicleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.Id);

        vehicle.Activate();

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
