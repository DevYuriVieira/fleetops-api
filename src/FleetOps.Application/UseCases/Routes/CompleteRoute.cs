namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record CompleteRouteCommand(Guid RouteId, DateTimeOffset? ActualArrival = null);

public sealed class CompleteRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CompleteRouteUseCase(IRouteRepository routeRepository, IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(CompleteRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken)
            ?? throw new NotFoundException("Route", command.RouteId);

        var arrivalTime = command.ActualArrival ?? DateTimeOffset.UtcNow;
        route.Complete(arrivalTime);

        await _routeRepository.UpdateAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
