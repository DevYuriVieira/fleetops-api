namespace FleetOps.Application.UseCases.Deliveries;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Enums;

public sealed record AssignDeliveryCommand(
    Guid DeliveryId,
    Guid VehicleId,
    Guid DriverId,
    DateTimeOffset EstimatedDeliveryTime);

public sealed class AssignDeliveryUseCase
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AssignDeliveryUseCase(
        IDeliveryRepository deliveryRepository,
        IVehicleRepository vehicleRepository,
        IDriverRepository driverRepository,
        IUnitOfWork unitOfWork)
    {
        _deliveryRepository = deliveryRepository;
        _vehicleRepository = vehicleRepository;
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeliveryDto> ExecuteAsync(AssignDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new NotFoundException("Delivery", command.DeliveryId);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken)
            ?? throw new NotFoundException("Vehicle", command.VehicleId);

        if (vehicle.Status != VehicleStatus.Active)
        {
            throw new ConflictException($"Vehicle '{vehicle.Id}' cannot be assigned because vehicle status is {vehicle.Status}.");
        }

        if (vehicle.CapacityKg < delivery.WeightKg)
        {
            throw new ConflictException($"Vehicle capacity ({vehicle.CapacityKg} kg) is insufficient for delivery weight ({delivery.WeightKg} kg).");
        }

        var driver = await _driverRepository.GetByIdAsync(command.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", command.DriverId);

        if (driver.Status != DriverStatus.Active)
        {
            throw new ConflictException($"Driver '{driver.Id}' cannot be assigned because driver status is {driver.Status}.");
        }

        delivery.Assign(command.VehicleId, command.DriverId, command.EstimatedDeliveryTime);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return delivery.ToDto();
    }
}
