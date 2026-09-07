namespace FleetOps.Infrastructure.Messaging;

public interface IRabbitMqPublisher
{
    Task PublishAsync(
        Guid messageId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default);

    Task PublishAsync(
        Guid messageId,
        string eventType,
        string routingKey,
        string payload,
        string? traceParent,
        CancellationToken cancellationToken = default);
}
