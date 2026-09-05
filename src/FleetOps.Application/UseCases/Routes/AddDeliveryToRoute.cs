namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record AddDeliveryToRouteCommand(Guid RouteId, Guid DeliveryId);

public sealed class AddDeliveryToRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IDeliveryRepository _deliveryRepository;
    private readonly IUnitOfWork _unitOfWork;

    public AddDeliveryToRouteUseCase(
        IRouteRepository routeRepository,
        IDeliveryRepository deliveryRepository,
        IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _deliveryRepository = deliveryRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(AddDeliveryToRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken)
            ?? throw new NotFoundException("Route", command.RouteId);

        var delivery = await _deliveryRepository.GetByIdAsync(command.DeliveryId, cancellationToken)
            ?? throw new NotFoundException("Delivery", command.DeliveryId);

        route.AddDelivery(delivery.Id);

        await _routeRepository.UpdateAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
