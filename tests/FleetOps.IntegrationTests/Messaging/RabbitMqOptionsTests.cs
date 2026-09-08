namespace FleetOps.IntegrationTests.Messaging;

using FleetOps.Infrastructure.Configuration;
using Xunit;

public class RabbitMqOptionsTests
{
    [Fact]
    public void GetRetryQueueName_WithDefaultDelays_ReturnsSecondsSuffix()
    {
        var options = new RabbitMqOptions();

        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.10s", options.GetRetryQueueName(0));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.30s", options.GetRetryQueueName(1));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.90s", options.GetRetryQueueName(2));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.attempt-4", options.GetRetryQueueName(3));
    }

    [Fact]
    public void GetRetryQueueName_WithSubSecondDelays_ReturnsMillisecondsSuffix()
    {
        var options = new RabbitMqOptions
        {
            RetryDelaysMilliseconds = [150, 300, 450]
        };

        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.150ms", options.GetRetryQueueName(0));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.300ms", options.GetRetryQueueName(1));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.450ms", options.GetRetryQueueName(2));
        Assert.Equal("fleetops.vehicle-maintenance.completed.retry.attempt-4", options.GetRetryQueueName(3));
    }
}
