namespace FleetOps.Infrastructure.Services;

using System.Text.Json;
using FleetOps.Application.Abstractions.Events;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Persistence;
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
    private readonly OutboxOptions _options;

    public OutboxService(
        FleetOpsDbContext context,
        IOptions<OutboxOptions> options,
        IDomainEventDispatcher? dispatcher = null)
    {
        _context = context;
        _dispatcher = dispatcher;
        _options = options.Value;
    }

    public async Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
    {
        var messages = await _context.OutboxMessages
            .Where(m => m.ProcessedOnUtc == null && m.Attempts < _options.MaxAttempts)
            .OrderBy(m => m.OccurredOnUtc)
            .Take(_options.BatchSize)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
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

                message.MarkProcessed(DateTimeOffset.UtcNow);
                processedCount++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.RecordFailure(ex.ToString());
            }
        }

        await _context.SaveChangesAsync(cancellationToken);

        return processedCount;
    }
}
