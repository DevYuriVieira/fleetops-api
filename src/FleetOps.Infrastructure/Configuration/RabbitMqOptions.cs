namespace FleetOps.Infrastructure.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 5672;
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public string VirtualHost { get; set; } = "/";
    public string ExchangeName { get; set; } = "fleetops.events";
    public string DeadLetterExchangeName { get; set; } = "fleetops.events.dlx";
    public string MaintenanceQueueName { get; set; } = "fleetops.vehicle-maintenance.completed";
    public string MaintenanceDlqName { get; set; } = "fleetops.vehicle-maintenance.completed.dlq";
    public int MaxRetryAttempts { get; set; } = 3;
    public bool Enabled { get; set; } = true;
}
