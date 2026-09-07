namespace FleetOps.Infrastructure.Persistence.Entities;

using System.Text.Json;
using FleetOps.Domain.Events;
using FleetOps.Infrastructure.Services;

public sealed class OutboxMessage
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Guid Id { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public string EventType { get; private set; }
    public string Payload { get; private set; }
    public DateTimeOffset? ProcessedOnUtc { get; private set; }
    public int Attempts { get; private set; }
    public string? Error { get; private set; }
    public string? TraceParent { get; private set; }

    private OutboxMessage()
    {
        EventType = null!;
        Payload = null!;
    }

    public OutboxMessage(
        Guid id,
        DateTimeOffset occurredOnUtc,
        string eventType,
        string payload,
        string? traceParent = null)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        EventType = eventType;
        Payload = payload;
        TraceParent = traceParent;
        ProcessedOnUtc = null;
        Attempts = 0;
        Error = null;
    }

    public static OutboxMessage FromDomainEvent(IDomainEvent domainEvent, string? traceParent = null)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var eventType = OutboxEventRegistry.GetEventName(domainEvent.GetType());
        var payload = JsonSerializer.Serialize(domainEvent, domainEvent.GetType(), SerializerOptions);

        return new OutboxMessage(
            Guid.NewGuid(),
            domainEvent.OccurredOn,
            eventType,
            payload,
            traceParent);
    }

    public void MarkProcessed(DateTimeOffset processedOnUtc)
    {
        Attempts++;
        ProcessedOnUtc = processedOnUtc;
        Error = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        Error = error;
    }
}
