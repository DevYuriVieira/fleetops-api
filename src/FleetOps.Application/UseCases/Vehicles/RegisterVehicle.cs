namespace FleetOps.Application.UseCases.Vehicles;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;

public sealed record RegisterVehicleCommand(
    string LicensePlate,
    string Type,
    string Make,
    string Model,
    int Year,
    int Mileage,
    decimal CapacityKg);

public sealed class RegisterVehicleUseCase
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterVehicleUseCase(IVehicleRepository vehicleRepository, IUnitOfWork unitOfWork)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<VehicleDto> ExecuteAsync(RegisterVehicleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<VehicleType>(command.Type, true, out var vehicleType))
        {
            throw new ValidationException($"Invalid vehicle type: '{command.Type}'.");
        }

        var plate = LicensePlate.Create(command.LicensePlate);

        var exists = await _vehicleRepository.ExistsByLicensePlateAsync(plate.Value, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Vehicle with license plate '{plate.Value}' is already registered.");
        }

        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            plate,
            vehicleType,
            command.Make,
            command.Model,
            command.Year,
            command.Mileage,
            command.CapacityKg);

        await _vehicleRepository.AddAsync(vehicle, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return vehicle.ToDto();
    }
}
