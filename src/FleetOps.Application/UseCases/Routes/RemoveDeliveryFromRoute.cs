namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record RemoveDeliveryFromRouteCommand(Guid RouteId, Guid DeliveryId);

public sealed class RemoveDeliveryFromRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RemoveDeliveryFromRouteUseCase(IRouteRepository routeRepository, IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(RemoveDeliveryFromRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken)
            ?? throw new NotFoundException("Route", command.RouteId);

        route.RemoveDelivery(command.DeliveryId);

        await _routeRepository.UpdateAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
