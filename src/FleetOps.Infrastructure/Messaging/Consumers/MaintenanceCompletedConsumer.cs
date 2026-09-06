namespace FleetOps.Infrastructure.Messaging.Consumers;

using System.Text;
using System.Text.Json;
using FleetOps.Application.UseCases.Maintenance;
using FleetOps.Domain.Events;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

public sealed class MaintenanceCompletedConsumer : BackgroundService
{
    private const string ConsumerName = "MaintenanceCompletedConsumer";
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly IRabbitMqConnection _connection;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<MaintenanceCompletedConsumer> _logger;

    public MaintenanceCompletedConsumer(
        IRabbitMqConnection connection,
        IServiceScopeFactory scopeFactory,
        IOptions<RabbitMqOptions> options,
        ILogger<MaintenanceCompletedConsumer> logger)
    {
        _connection = connection;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection.InitializeTopologyAsync(stoppingToken);

                var channelOptions = new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true);

                var channel = await _connection.CreateChannelAsync(channelOptions, cancellationToken: stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 10, global: false, cancellationToken: stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                consumer.ReceivedAsync += async (_, ea) =>
                {
                    await HandleMessageAsync(channel, ea, stoppingToken);
                };

                await channel.BasicConsumeAsync(
                    queue: _options.MaintenanceQueueName,
                    autoAck: false,
                    consumer: consumer,
                    cancellationToken: stoppingToken);

                _logger.LogInformation("MaintenanceCompletedConsumer subscribed to queue {Queue}", _options.MaintenanceQueueName);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RabbitMQ consumer connection error. Retrying in 5 seconds...");
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
            }
        }
    }

    private async Task HandleMessageAsync(IChannel channel, BasicDeliverEventArgs ea, CancellationToken cancellationToken)
    {
        var messageId = ResolveMessageId(ea.BasicProperties);
        var body = Encoding.UTF8.GetString(ea.Body.ToArray());

        _logger.LogInformation(
            "Processing message {MessageId} from queue {Queue} with routing key {RoutingKey}",
            messageId,
            _options.MaintenanceQueueName,
            ea.RoutingKey);

        MaintenanceCompletedDomainEvent? domainEvent = null;
        var isPoison = false;
        string? poisonReason = null;

        try
        {
            domainEvent = JsonSerializer.Deserialize<MaintenanceCompletedDomainEvent>(body, SerializerOptions);
            if (domainEvent is null || domainEvent.MaintenanceId == Guid.Empty || domainEvent.VehicleId == Guid.Empty)
            {
                isPoison = true;
                poisonReason = "Payload is null or contains empty identifiers.";
            }
        }
        catch (Exception ex)
        {
            isPoison = true;
            poisonReason = ex.Message;
        }

        if (isPoison)
        {
            _logger.LogError("Permanent poison message {MessageId}: {Reason}. Routing to DLQ.", messageId, poisonReason);
            await RouteToDlqAsync(channel, ea, messageId, $"PoisonMessage: {poisonReason}", cancellationToken);
            return;
        }

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<FleetOpsDbContext>();
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessMaintenanceCompletedUseCase>();

            var strategy = dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await dbContext.Database.BeginTransactionAsync(cancellationToken);

                var alreadyProcessed = await dbContext.MaintenanceCompletionRecords
                    .AnyAsync(m => m.MessageId == messageId, cancellationToken);

                if (alreadyProcessed)
                {
                    _logger.LogInformation(
                        "Message {MessageId} already processed. Skipping duplicate execution.",
                        messageId);

                    await tx.RollbackAsync(cancellationToken);
                    return;
                }

                var command = new ProcessMaintenanceCompletedCommand(
                    messageId,
                    domainEvent!.MaintenanceId,
                    domainEvent.VehicleId,
                    domainEvent.CompletedAt);

                await useCase.ExecuteAsync(command, cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Durable maintenance completion record committed for message {MessageId} (Maintenance: {MaintenanceId}, Vehicle: {VehicleId})",
                    messageId,
                    domainEvent.MaintenanceId,
                    domainEvent.VehicleId);
            });

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleTransientFailureAsync(channel, ea, messageId, ex, cancellationToken);
        }
    }

    private async Task HandleTransientFailureAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        Guid messageId,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var currentRetryCount = GetRetryCount(ea.BasicProperties);
        _logger.LogWarning(
            exception,
            "Transient failure processing message {MessageId}. Current retry count: {RetryCount}/{MaxRetries}",
            messageId,
            currentRetryCount,
            _options.MaxRetryAttempts);

        if (currentRetryCount >= _options.MaxRetryAttempts || currentRetryCount >= _options.RetryDelaysMilliseconds.Length)
        {
            _logger.LogError(
                "Exhausted maximum retry attempts ({MaxRetries}) for message {MessageId}. Routing to DLQ.",
                _options.MaxRetryAttempts,
                messageId);

            await RouteToDlqAsync(channel, ea, messageId, "MaxRetriesExhausted", cancellationToken);
            return;
        }

        var nextRetryCount = currentRetryCount + 1;
        var retryQueueName = _options.GetRetryQueueName(currentRetryCount);

        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType ?? "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = ea.BasicProperties.Timestamp
        };

        var headers = new Dictionary<string, object?>(ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            ["x-retry-count"] = nextRetryCount,
            ["x-exception-message"] = exception.Message
        };
        properties.Headers = headers;

        _logger.LogInformation(
            "Routing message {MessageId} to retry queue {RetryQueue} (attempt {NextRetry}/{MaxRetries})",
            messageId,
            retryQueueName,
            nextRetryCount,
            _options.MaxRetryAttempts);

        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: retryQueueName,
            mandatory: true,
            basicProperties: properties,
            body: ea.Body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Broker confirmed publication of retry message {MessageId} to queue {RetryQueue}",
            messageId,
            retryQueueName);

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
    }

    private async Task RouteToDlqAsync(
        IChannel channel,
        BasicDeliverEventArgs ea,
        Guid messageId,
        string reason,
        CancellationToken cancellationToken)
    {
        var properties = new BasicProperties
        {
            MessageId = messageId.ToString(),
            Type = ea.BasicProperties.Type,
            ContentType = ea.BasicProperties.ContentType ?? "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            Timestamp = ea.BasicProperties.Timestamp
        };

        var headers = new Dictionary<string, object?>(ea.BasicProperties.Headers ?? new Dictionary<string, object?>())
        {
            ["x-dlq-reason"] = reason,
            ["x-retry-count"] = GetRetryCount(ea.BasicProperties)
        };
        properties.Headers = headers;

        await channel.BasicPublishAsync(
            exchange: _options.DeadLetterExchangeName,
            routingKey: "maintenance.completed.dlq",
            mandatory: true,
            basicProperties: properties,
            body: ea.Body,
            cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Broker confirmed publication of message {MessageId} to DLQ",
            messageId);

        await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
    }

    private static Guid ResolveMessageId(IReadOnlyBasicProperties properties)
    {
        if (properties.MessageId is not null && Guid.TryParse(properties.MessageId, out var id))
        {
            return id;
        }

        return Guid.NewGuid();
    }

    private static int GetRetryCount(IReadOnlyBasicProperties properties)
    {
        if (properties.Headers is not null &&
            properties.Headers.TryGetValue("x-retry-count", out var value) &&
            value is not null)
        {
            if (value is int count)
            {
                return count;
            }

            if (value is long longCount)
            {
                return (int)longCount;
            }

            if (value is byte[] bytes && int.TryParse(Encoding.UTF8.GetString(bytes), out var parsedBytes))
            {
                return parsedBytes;
            }

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        return 0;
    }
}
