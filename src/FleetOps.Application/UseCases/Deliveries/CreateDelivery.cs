namespace FleetOps.Application.UseCases.Deliveries;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;

public sealed record CreateDeliveryCommand(
    string TrackingCode,
    AddressDto Origin,
    AddressDto Destination,
    string Priority,
    decimal WeightKg);

public sealed class CreateDeliveryUseCase
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateDeliveryUseCase(IDeliveryRepository deliveryRepository, IUnitOfWork unitOfWork)
    {
        _deliveryRepository = deliveryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeliveryDto> ExecuteAsync(CreateDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!Enum.TryParse<DeliveryPriority>(command.Priority, true, out var priority))
        {
            throw new ValidationException($"Invalid delivery priority: '{command.Priority}'.");
        }

        var trackingCode = TrackingCode.Create(command.TrackingCode);

        var exists = await _deliveryRepository.ExistsByTrackingCodeAsync(trackingCode.Value, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Delivery with tracking code '{trackingCode.Value}' already exists.");
        }

        var origin = command.Origin.ToDomain();
        var destination = command.Destination.ToDomain();

        var delivery = Delivery.Create(
            Guid.NewGuid(),
            trackingCode,
            origin,
            destination,
            priority,
            command.WeightKg);

        await _deliveryRepository.AddAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return delivery.ToDto();
    }
}
