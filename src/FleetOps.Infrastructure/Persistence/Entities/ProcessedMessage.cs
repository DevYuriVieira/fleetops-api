namespace FleetOps.Infrastructure.Persistence.Entities;

public sealed class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; }
    public DateTimeOffset ProcessedOnUtc { get; private set; }

    private ProcessedMessage()
    {
        Consumer = null!;
    }

    public ProcessedMessage(Guid messageId, string consumer, DateTimeOffset processedOnUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumer);

        MessageId = messageId;
        Consumer = consumer;
        ProcessedOnUtc = processedOnUtc;
    }
}
