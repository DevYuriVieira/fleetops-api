namespace FleetOps.Application.Abstractions.Events;

using FleetOps.Domain.Events;

public interface IDomainEventDispatcher
{
    Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default);
}
