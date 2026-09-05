namespace FleetOps.Application.UseCases.Deliveries;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record StartDeliveryCommand(Guid DeliveryId);

public sealed class StartDeliveryUseCase
{
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartDeliveryUseCase(IDeliveryRepository deliveryRepository, IUnitOfWork unitOfWork)
    {
        _deliveryRepository = deliveryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DeliveryDto> ExecuteAsync(StartDeliveryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new NotFoundException("Delivery", command.DeliveryId);

        delivery.Start();

        await _deliveryRepository.UpdateAsync(delivery, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return delivery.ToDto();
    }
}
