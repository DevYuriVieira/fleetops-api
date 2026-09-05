namespace FleetOps.Application.UseCases.Routes;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Domain.Entities;

public sealed record CreateRouteCommand(
    AddressDto Origin,
    AddressDto Destination,
    DateTimeOffset PlannedDeparture,
    DateTimeOffset EstimatedArrival);

public sealed class CreateRouteUseCase
{
    private readonly IRouteRepository _routeRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateRouteUseCase(IRouteRepository routeRepository, IUnitOfWork unitOfWork)
    {
        _routeRepository = routeRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<RouteDto> ExecuteAsync(CreateRouteCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var origin = command.Origin.ToDomain();
        var destination = command.Destination.ToDomain();

        var route = Route.Create(
            Guid.NewGuid(),
            origin,
            destination,
            command.PlannedDeparture,
            command.EstimatedArrival);

        await _routeRepository.AddAsync(route, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return route.ToDto();
    }
}
