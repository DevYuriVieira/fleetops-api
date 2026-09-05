namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record StartRouteCommand(Guid RouteId, DateTimeOffset? ActualDeparture = null);

public sealed class StartRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public StartRouteUseCase(IRouteRepository routeRepository, IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(StartRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var route = await _routeRepository.GetByIdAsync(command.RouteId, cancellationToken)
            ?? throw new NotFoundException("Route", command.RouteId);

        var departureTime = command.ActualDeparture ?? DateTimeOffset.UtcNow;
        route.Start(departureTime);

        await _routeRepository.UpdateAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
