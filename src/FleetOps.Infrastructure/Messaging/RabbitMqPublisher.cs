namespace FleetOps.Infrastructure.Messaging;

using System.Text;
using FleetOps.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

public sealed class RabbitMqPublisher : IRabbitMqPublisher
{
    private readonly IRabbitMqConnection _connection;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqPublisher> _logger;

    public RabbitMqPublisher(
        IRabbitMqConnection connection,
        IOptions<RabbitMqOptions> options,
        ILogger<RabbitMqPublisher> logger)
    {
        _connection = connection;
        _options = options.Value;
        _logger = logger;
    }

    public Task PublishAsync(
        Guid messageId,
        string eventType,
        string routingKey,
        string payload,
        CancellationToken cancellationToken = default)
    {
        return PublishAsync(messageId, eventType, routingKey, payload, traceParent: null, cancellationToken);
    }

    public async Task PublishAsync(
        Guid messageId,
        string eventType,
        string routingKey,
        string payload,
        string? traceParent,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(routingKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);

        await _connection.InitializeTopologyAsync(cancellationToken);

        var channelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: true,
            publisherConfirmationTrackingEnabled: true);

        await using var channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken);

        var body = Encoding.UTF8.GetBytes(payload);

        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            Type = eventType,
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        var resolvedTraceParent = traceParent ?? System.Diagnostics.Activity.Current?.Id;
        if (!string.IsNullOrWhiteSpace(resolvedTraceParent))
        {
            properties.Headers = new Dictionary<string, object?>
            {
                ["traceparent"] = resolvedTraceParent
            };
        }

        _logger.LogInformation(
            "Publishing outbox message {MessageId} of type {EventType} with routing key {RoutingKey} to exchange {Exchange}",
            messageId,
            eventType,
            routingKey,
            _options.ExchangeName);

        try
        {
            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: routingKey,
                mandatory: true,
                basicProperties: properties,
                body: body,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Broker confirmed publication of outbox message {MessageId} of type {EventType}",
                messageId,
                eventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to publish outbox message {MessageId} of type {EventType} to RabbitMQ",
                messageId,
                eventType);

            throw;
        }
    }
}
