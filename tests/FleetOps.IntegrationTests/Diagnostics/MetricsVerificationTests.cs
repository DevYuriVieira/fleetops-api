namespace FleetOps.IntegrationTests.Diagnostics;

using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using System.Text.Json;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Diagnostics;
using FleetOps.Infrastructure.Messaging;
using FleetOps.Infrastructure.Messaging.Consumers;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Persistence.Entities;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.Infrastructure.Services;
using FleetOps.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

public sealed class MetricsVerificationTests : BaseIntegrationTest
{
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;

    public MetricsVerificationTests()
    {
        _rabbitOptions = new RabbitMqOptions
        {
            Host = Environment.GetEnvironmentVariable("FLEETOPS_RABBITMQ_HOST") ?? "127.0.0.1",
            Port = int.TryParse(Environment.GetEnvironmentVariable("FLEETOPS_RABBITMQ_PORT"), out var p) ? p : 5672,
            Username = "guest",
            Password = "guest",
            VirtualHost = "/",
            ExchangeName = "fleetops.events",
            DeadLetterExchangeName = "fleetops.events.dlx",
            MaintenanceQueueName = "fleetops.vehicle-maintenance.completed",
            MaintenanceDlqName = "fleetops.vehicle-maintenance.completed.dlq",
            MaxRetryAttempts = 3,
            RetryDelaysMilliseconds = [150, 300, 450],
            Enabled = true
        };

        var optionsWrapper = Options.Create(_rabbitOptions);
        _connection = new RabbitMqConnection(optionsWrapper, NullLogger<RabbitMqConnection>.Instance);
        _publisher = new RabbitMqPublisher(_connection, optionsWrapper, NullLogger<RabbitMqPublisher>.Instance);
    }

    public override async Task InitializeAsync()
    {
        await base.InitializeAsync();
        await _connection.InitializeTopologyAsync();
        await PurgeQueuesAsync();
    }

    public override async Task DisposeAsync()
    {
        await PurgeQueuesAsync();
        await _connection.DisposeAsync();
        await base.DisposeAsync();
    }

    private async Task PurgeQueuesAsync()
    {
        try
        {
            await using var channel = await _connection.CreateChannelAsync();
            await channel.QueuePurgeAsync(_rabbitOptions.MaintenanceQueueName);
            await channel.QueuePurgeAsync(_rabbitOptions.MaintenanceDlqName);
            for (var i = 0; i < _rabbitOptions.RetryDelaysMilliseconds.Length; i++)
            {
                await channel.QueuePurgeAsync(_rabbitOptions.GetRetryQueueName(i));
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task OutboxPublishing_EmitsOutboxPublishedTotal_AndDurationMetrics()
    {
        // Arrange
        var recordedMetrics = new ConcurrentBag<(string InstrumentName, double Value, Dictionary<string, object?> Tags)>();
        using var meterListener = new MeterListener();

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == FleetOpsMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            recordedMetrics.Add((instrument.Name, measurement, tagDict));
        });

        meterListener.SetMeasurementEventCallback<double>((instrument, measurement, tags, state) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            recordedMetrics.Add((instrument.Name, measurement, tagDict));
        });

        meterListener.Start();

        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(450m, "USD"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var message = new OutboxMessage(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "MaintenanceCompleted",
            payload,
            null);

        await using (var context = CreateDbContext())
        {
            await context.OutboxMessages.AddAsync(message);
            await context.SaveChangesAsync();
        }

        // Act: Process outbox message
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(
                context,
                Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 }),
                rabbitMqPublisher: _publisher);

            var processed = await service.ProcessPendingMessagesAsync(CancellationToken.None);
            Assert.Equal(1, processed);
        }

        meterListener.RecordObservableInstruments();

        // Assert: Counter and Histogram were recorded
        var publishedCounter = recordedMetrics.FirstOrDefault(m => m.InstrumentName == "fleetops.outbox.messages.published.total");
        Assert.NotEqual(default, publishedCounter);
        Assert.Equal(1, publishedCounter.Value);
        Assert.Equal("MaintenanceCompleted", publishedCounter.Tags["event_type"]);
        Assert.Equal("success", publishedCounter.Tags["status"]);

        var durationHistogram = recordedMetrics.FirstOrDefault(m => m.InstrumentName == "fleetops.outbox.publish.duration.ms");
        Assert.NotEqual(default, durationHistogram);
        Assert.True(durationHistogram.Value > 0);
    }

    [Fact]
    public async Task DuplicateDelivery_EmitsConsumerIdempotencyHitsMetric()
    {
        // Arrange
        var recordedMetrics = new ConcurrentBag<(string InstrumentName, double Value, Dictionary<string, object?> Tags)>();
        using var meterListener = new MeterListener();

        meterListener.InstrumentPublished = (instrument, listener) =>
        {
            if (instrument.Meter.Name == FleetOpsMetrics.MeterName)
            {
                listener.EnableMeasurementEvents(instrument);
            }
        };

        meterListener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            var tagDict = new Dictionary<string, object?>();
            foreach (var tag in tags)
            {
                tagDict[tag.Key] = tag.Value;
            }
            recordedMetrics.Add((instrument.Name, measurement, tagDict));
        });

        meterListener.Start();

        var messageId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(500m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Pre-insert completion record to simulate pre-existing processed message
        await using (var context = CreateDbContext())
        {
            var record = new MaintenanceCompletionRecord(messageId, maintenanceId, vehicleId, completedAt, DateTimeOffset.UtcNow);
            await context.MaintenanceCompletionRecords.AddAsync(record);
            await context.SaveChangesAsync();
        }

        // Publish to RabbitMQ
        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload);

        // Start consumer
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_rabbitOptions),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        // Wait for consumer to process duplicate message
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            if (recordedMetrics.Any(m => m.InstrumentName == "fleetops.consumer.idempotency.hits.total"))
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        // Assert: Idempotency hit metric was recorded
        var hitMetric = recordedMetrics.FirstOrDefault(m => m.InstrumentName == "fleetops.consumer.idempotency.hits.total");
        Assert.NotEqual(default, hitMetric);
        Assert.Equal(1, hitMetric.Value);
        Assert.Equal("MaintenanceCompleted", hitMetric.Tags["event_type"]);
    }

    private IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddDbContext<FleetOpsDbContext>((_, options) =>
        {
            options.UseNpgsql(ConnectionString);
        });

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
        services.AddScoped<IMaintenanceCompletionRecordRepository, MaintenanceCompletionRecordRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<FleetOps.Application.UseCases.Maintenance.ProcessMaintenanceCompletedUseCase>();

        return services.BuildServiceProvider();
    }
}
