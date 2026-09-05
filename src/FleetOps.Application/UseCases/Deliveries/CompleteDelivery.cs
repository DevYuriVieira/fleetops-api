namespace FleetOps.Application.UseCases.Deliveries;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record CompleteDeliveryCommand(Guid DeliveryId, DateTimeOffset? ActualDeliveryTime = null);

public sealed class CompleteDeliveryUseCase
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteDeliveryUseCase(IDeliveryRepository deliveryRepository, IUnitOfWork unitOfWork)
    {
        _deliveryRepository = deliveryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeliveryDto> ExecuteAsync(CompleteDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new NotFoundException("Delivery", command.DeliveryId);

        var completionTime = command.ActualDeliveryTime ?? DateTimeOffset.UtcNow;
        delivery.Complete(completionTime);

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return delivery.ToDto();
    }
}
