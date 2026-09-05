namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record UnassignDriverFromVehicleCommand(Guid VehicleId);

public sealed class UnassignDriverFromVehicleUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public UnassignDriverFromVehicleUseCase(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(UnassignDriverFromVehicleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        vehicle.UnassignDriver();

        await _vehicleRepository.UpdateAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
