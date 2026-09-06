namespace FleetOps.Infrastructure.Messaging.Consumers;

using System.Text;
using System.Text.Json;
using FleetOps.Application.UseCases.Maintenance;
using FleetOps.Domain.Events;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Persistence.Entities;
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

                var channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
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

        MaintenanceCompletedDomainEvent? domainEvent;
        try
        {
            domainEvent = JsonSerializer.Deserialize<MaintenanceCompletedDomainEvent>(body, SerializerOptions);
            if (domainEvent is null)
            {
                throw new JsonException("Deserialized domain event is null.");
            }
        }
        catch (JsonException ex)
        {
            _logger.LogError(
                ex,
                "Poison message {MessageId}: invalid payload format. Rejecting to DLQ without requeue.",
                messageId);

            await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken);
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

                var alreadyProcessed = await dbContext.ProcessedMessages
                    .AnyAsync(m => m.MessageId == messageId && m.Consumer == ConsumerName, cancellationToken);

                if (alreadyProcessed)
                {
                    _logger.LogInformation(
                        "Message {MessageId} already processed by {Consumer}. Skipping duplicate execution.",
                        messageId,
                        ConsumerName);

                    await tx.RollbackAsync(cancellationToken);
                    return;
                }

                var command = new ProcessMaintenanceCompletedCommand(domainEvent.MaintenanceId, domainEvent.VehicleId);
                await useCase.ExecuteAsync(command, cancellationToken);

                dbContext.ProcessedMessages.Add(new ProcessedMessage(messageId, ConsumerName, DateTimeOffset.UtcNow));
                await dbContext.SaveChangesAsync(cancellationToken);

                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation(
                    "Durable state committed for message {MessageId} (Maintenance: {MaintenanceId}, Vehicle: {VehicleId})",
                    messageId,
                    domainEvent.MaintenanceId,
                    domainEvent.VehicleId);
            });

            await channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            var retryCount = GetRetryCount(ea.BasicProperties);
            _logger.LogWarning(
                ex,
                "Transient failure processing message {MessageId}. Attempt: {RetryCount}/{MaxRetries}",
                messageId,
                retryCount + 1,
                _options.MaxRetryAttempts);

            if (retryCount + 1 >= _options.MaxRetryAttempts)
            {
                _logger.LogError(
                    "Exhausted maximum retry attempts ({MaxRetries}) for message {MessageId}. Routing to DLQ.",
                    _options.MaxRetryAttempts,
                    messageId);

                await channel.BasicRejectAsync(ea.DeliveryTag, requeue: false, cancellationToken);
            }
            else
            {
                await channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: true, cancellationToken);
            }
        }
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

            if (int.TryParse(value.ToString(), out var parsed))
            {
                return parsed;
            }
        }

        if (properties.Headers is not null &&
            properties.Headers.TryGetValue("x-death", out var xDeath) &&
            xDeath is System.Collections.IEnumerable list)
        {
            var deaths = 0;
            foreach (var _ in list)
            {
                deaths++;
            }
            return deaths;
        }

        return 0;
    }
}
