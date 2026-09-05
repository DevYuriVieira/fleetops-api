namespace FleetOps.Domain.Events;

public interface IDomainEvent
{
    DateTimeOffset OccurredOn { get; }
}
