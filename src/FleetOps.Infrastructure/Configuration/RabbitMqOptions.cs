namespace FleetOps.Infrastructure.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "fleetops.events";
    public string DeadLetterExchangeName { get; set; } = "fleetops.events.dlx";
    public string MaintenanceQueueName { get; set; } = "fleetops.vehicle-maintenance.completed";
    public string MaintenanceDlqName { get; set; } = "fleetops.vehicle-maintenance.completed.dlq";
    public int MaxRetryAttempts { get; set; } = 3;
    public int[] RetryDelaysMilliseconds { get; set; } = [10000, 30000, 90000];
    public bool Enabled { get; set; } = true;

    public string GetRetryQueueName(int attemptIndex)
    {
        string suffix;
        if (attemptIndex < RetryDelaysMilliseconds.Length)
        {
            var delayMs = RetryDelaysMilliseconds[attemptIndex];
            suffix = delayMs >= 1000 && delayMs % 1000 == 0
                ? $"{delayMs / 1000}s"
                : $"{delayMs}ms";
        }
        else
        {
            suffix = $"attempt-{attemptIndex + 1}";
        }

        return $"{MaintenanceQueueName}.retry.{suffix}";
    }
}
