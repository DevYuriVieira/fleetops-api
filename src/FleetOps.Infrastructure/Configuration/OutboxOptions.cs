namespace FleetOps.Infrastructure.Configuration;

public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 20;
    public double IntervalSeconds { get; set; } = 2.0;
    public int MaxAttempts { get; set; } = 5;
    public bool Enabled { get; set; } = true;
}
