namespace FleetOps.Infrastructure.Services;

using System.Text.Json;
using FleetOps.Application.Abstractions.Events;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Messaging;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

public sealed class OutboxService : IOutboxService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    private readonly FleetOpsDbContext _context;
    private readonly IDomainEventDispatcher? _dispatcher;
    private readonly IRabbitMqPublisher? _rabbitMqPublisher;
    private readonly OutboxOptions _options;

    public OutboxService(
        FleetOpsDbContext context,
        IOptions<OutboxOptions> options,
        IDomainEventDispatcher? dispatcher = null,
        IRabbitMqPublisher? rabbitMqPublisher = null)
    {
        _context = context;
        _dispatcher = dispatcher;
        _rabbitMqPublisher = rabbitMqPublisher;
        _options = options.Value;
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            List<OutboxMessage> messages;

            if (_context.Database.IsNpgsql())
            {
                messages = await _context.OutboxMessages
                    .FromSqlRaw(
                        """
                        SELECT * FROM outbox_messages
                        WHERE processed_on_utc IS NULL AND attempts < {0}
                        ORDER BY occurred_on_utc
                        LIMIT {1}
                        FOR UPDATE SKIP LOCKED
                        """,
                        _options.MaxAttempts,
                        _options.BatchSize)
                    .ToListAsync(cancellationToken);
            }
            else
            {
                messages = await _context.OutboxMessages
                    .Where(m => m.ProcessedOnUtc == null && m.Attempts < _options.MaxAttempts)
                    .OrderBy(m => m.OccurredOnUtc)
                    .Take(_options.BatchSize)
                    .ToListAsync(cancellationToken);
            }

            if (messages.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken);
                return 0;
            }

            var processedCount = 0;

            foreach (var message in messages)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var domainEvent = OutboxEventRegistry.Deserialize(message.EventType, message.Payload, SerializerOptions);

                    if (domainEvent is null)
                    {
                        message.RecordFailure($"Unknown or non-deserializable domain event type: '{message.EventType}'.");
                        continue;
                    }

                    if (_dispatcher is not null)
                    {
                        await _dispatcher.DispatchEventsAsync([domainEvent], cancellationToken);
                    }

                    if (_rabbitMqPublisher is not null)
                    {
                        var routingKey = ResolveRoutingKey(message.EventType);
                        await _rabbitMqPublisher.PublishAsync(
                            message.Id,
                            message.EventType,
                            routingKey,
                            message.Payload,
                            cancellationToken: cancellationToken);
                    }

                    message.MarkProcessed(DateTimeOffset.UtcNow);
                    processedCount++;
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    message.RecordFailure(ex.ToString());
                }
            }

            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return processedCount;
        });
    }

    private static string ResolveRoutingKey(string eventType)
    {
        return eventType switch
        {
            "MaintenanceCompleted" => "maintenance.completed",
            "MaintenanceStarted" => "maintenance.started",
            "MaintenanceCancelled" => "maintenance.cancelled",
            "VehicleActivated" => "vehicle.activated",
            "VehicleDeactivated" => "vehicle.deactivated",
            "VehicleMileageUpdated" => "vehicle.mileage-updated",
            "VehicleSentToMaintenance" => "vehicle.sent-to-maintenance",
            "VehicleReturnedFromMaintenance" => "vehicle.returned-from-maintenance",
            "DriverAssignedToVehicle" => "driver.assigned-to-vehicle",
            "DriverUnassignedFromVehicle" => "driver.unassigned-from-vehicle",
            "DeliveryAssigned" => "delivery.assigned",
            "DeliveryStarted" => "delivery.started",
            "DeliveryCompleted" => "delivery.completed",
            "DeliveryCancelled" => "delivery.cancelled",
            _ => eventType.ToLowerInvariant()
        };
    }
}
