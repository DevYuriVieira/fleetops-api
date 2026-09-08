namespace FleetOps.IntegrationTests.Diagnostics;

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FleetOps.Application.Abstractions.Events;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.UseCases.Maintenance;
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

public sealed class TracingPropagationTests : BaseIntegrationTest
{
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;

    public TracingPropagationTests()
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
    public async Task DbContext_WhenSavingDomainEvents_CapturesActiveSpanTraceParent_IntoOutboxMessage()
    {
        // Arrange
        var rootActivity = new Activity("Test.HttpRequest");
        rootActivity.SetIdFormat(ActivityIdFormat.W3C);
        rootActivity.Start();

        var expectedTraceId = rootActivity.TraceId.ToHexString();
        var expectedSpanId = rootActivity.SpanId.ToHexString();

        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId,
            LicensePlate.Create("TRC1001"),
            VehicleType.Van,
            "Ford",
            "Transit",
            2023,
            5000,
            2000m);

        vehicle.UpdateMileage(5500);

        try
        {
            // Act: SaveChanges captures System.Diagnostics.Activity.Current?.Id
            await using (var context = CreateDbContext())
            {
                var repo = new VehicleRepository(context);
                var uow = CreateUnitOfWork(context);
                await repo.AddAsync(vehicle);
                await uow.SaveChangesAsync();
            }
        }
        finally
        {
            rootActivity.Stop();
        }

        // Assert: OutboxMessage.TraceParent is populated with W3C traceparent
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "VehicleMileageUpdated" && m.Payload.Contains(vehicleId.ToString()));

            Assert.NotNull(msg);
            Assert.NotNull(msg.TraceParent);

            Assert.True(ActivityContext.TryParse(msg.TraceParent, null, out var parsedContext));
            Assert.Equal(expectedTraceId, parsedContext.TraceId.ToHexString());
            Assert.Equal(expectedSpanId, parsedContext.SpanId.ToHexString());
        }
    }

    [Fact]
    public async Task OutboxService_WhenProcessingMessage_PropagatesTraceId_AndLinksParentSpan()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom();
        var parentSpanId = ActivitySpanId.CreateRandom();
        var traceParent = $"00-{traceId.ToHexString()}-{parentSpanId.ToHexString()}-01";

        var vehicleId = Guid.NewGuid();
        var domainEvent = new VehicleMileageUpdatedDomainEvent(vehicleId, 10000, 12000, DateTimeOffset.UtcNow);
        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var message = new OutboxMessage(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "VehicleMileageUpdated",
            payload,
            traceParent);

        await using (var context = CreateDbContext())
        {
            await context.OutboxMessages.AddAsync(message);
            await context.SaveChangesAsync();
        }

        var capturedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FleetOpsDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => capturedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var spyPublisher = new SpyRabbitMqPublisher();
        var outboxOptions = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 });

        // Act
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, outboxOptions, rabbitMqPublisher: spyPublisher);
            var processed = await service.ProcessPendingMessagesAsync(CancellationToken.None);
            Assert.Equal(1, processed);
        }

        // Assert: OutboxService started an activity linked to the parent context
        var outboxActivity = capturedActivities.FirstOrDefault(a => a.OperationName == "OutboxService.ProcessMessage");
        Assert.NotNull(outboxActivity);
        Assert.Equal(traceId.ToHexString(), outboxActivity.TraceId.ToHexString());
        Assert.Equal(parentSpanId.ToHexString(), outboxActivity.ParentSpanId.ToHexString());
        Assert.NotEqual(parentSpanId.ToHexString(), outboxActivity.SpanId.ToHexString());

        // Assert: The traceParent passed to publisher reflects the outbox producer span
        Assert.Single(spyPublisher.PublishedCalls);
        var publishedCall = spyPublisher.PublishedCalls[0];
        Assert.NotNull(publishedCall.TraceParent);
        Assert.True(ActivityContext.TryParse(publishedCall.TraceParent, null, out var publishedContext));
        Assert.Equal(traceId.ToHexString(), publishedContext.TraceId.ToHexString());
        Assert.Equal(outboxActivity.SpanId.ToHexString(), publishedContext.SpanId.ToHexString());
    }

    [Fact]
    public async Task MaintenanceCompletedConsumer_WhenConsumingMessageWithTraceparentHeader_MaintainsDistributedLineage()
    {
        // Arrange
        var traceId = ActivityTraceId.CreateRandom();
        var producerSpanId = ActivitySpanId.CreateRandom();
        var traceParent = $"00-{traceId.ToHexString()}-{producerSpanId.ToHexString()}-01";

        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(850m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var capturedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FleetOpsDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => capturedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        // Publish to RabbitMQ with explicit traceParent header
        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload,
            traceParent: traceParent);

        // Act: Start Consumer to process message
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_rabbitOptions),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        MaintenanceCompletionRecord? record = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var context = CreateDbContext();
            record = await context.MaintenanceCompletionRecords.FindAsync(messageId);
            if (record is not null)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        // Assert: Record created
        Assert.NotNull(record);

        // Assert: Consumer activity extracted traceparent and preserved trace hierarchy
        var consumerActivity = capturedActivities.FirstOrDefault(a => a.OperationName == "MaintenanceCompletedConsumer.Process");
        Assert.NotNull(consumerActivity);
        Assert.Equal(traceId.ToHexString(), consumerActivity.TraceId.ToHexString());
        Assert.Equal(producerSpanId.ToHexString(), consumerActivity.ParentSpanId.ToHexString());
        Assert.NotEqual(producerSpanId.ToHexString(), consumerActivity.SpanId.ToHexString());
    }

    [Fact]
    public async Task EndToEnd_DistributedTracing_Preserves_TraceId_And_ParentChild_Hierarchy_Across_All_Three_Tiers()
    {
        // Arrange: Start root activity representing incoming HTTP command
        var rootActivity = new Activity("Http.CompleteMaintenanceEndpoint");
        rootActivity.SetIdFormat(ActivityIdFormat.W3C);
        rootActivity.Start();

        var rootTraceId = rootActivity.TraceId.ToHexString();
        var rootSpanId = rootActivity.SpanId.ToHexString();

        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("E2E-9999"),
            VehicleType.Truck,
            "Scania",
            "R500",
            2024,
            20000,
            40000m);

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            MaintenanceType.Preventive,
            "Major Overhaul",
            DateTimeOffset.UtcNow.AddDays(1));
        maintenance.Start(DateTimeOffset.UtcNow.AddHours(-2));
        maintenance.Complete(DateTimeOffset.UtcNow, new Money(2500m, "USD"));

        // Stage 1: DbContext SaveChanges captures Root Span into Outbox
        try
        {
            await using var context = CreateDbContext();
            await context.Vehicles.AddAsync(vehicle);
            await context.Maintenances.AddAsync(maintenance);
            await context.SaveChangesAsync();
        }
        finally
        {
            rootActivity.Stop();
        }

        Guid outboxId;
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages.FirstOrDefaultAsync(m => m.EventType == "MaintenanceCompleted");
            Assert.NotNull(msg);
            Assert.NotNull(msg.TraceParent);
            Assert.True(ActivityContext.TryParse(msg.TraceParent, null, out var dbContextCtx));
            Assert.Equal(rootTraceId, dbContextCtx.TraceId.ToHexString());
            Assert.Equal(rootSpanId, dbContextCtx.SpanId.ToHexString());
            outboxId = msg.Id;
        }

        // Stage 2: OutboxService processes message and publishes to RabbitMQ
        var capturedActivities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == FleetOpsDiagnostics.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => capturedActivities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(
                context,
                Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 }),
                rabbitMqPublisher: _publisher);

            var processed = await service.ProcessPendingMessagesAsync(CancellationToken.None);
            Assert.True(processed >= 1);
        }

        var outboxActivity = capturedActivities.FirstOrDefault(a => a.OperationName == "OutboxService.ProcessMessage");
        Assert.NotNull(outboxActivity);
        Assert.Equal(rootTraceId, outboxActivity.TraceId.ToHexString());
        Assert.Equal(rootSpanId, outboxActivity.ParentSpanId.ToHexString());
        var outboxSpanId = outboxActivity.SpanId.ToHexString();

        // Stage 3: Consumer consumes from RabbitMQ and runs business logic
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_rabbitOptions),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        MaintenanceCompletionRecord? record = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var context = CreateDbContext();
            record = await context.MaintenanceCompletionRecords.FindAsync(outboxId);
            if (record is not null)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        Assert.NotNull(record);

        var consumerActivity = capturedActivities.FirstOrDefault(a => a.OperationName == "MaintenanceCompletedConsumer.Process");
        Assert.NotNull(consumerActivity);

        // Verification of 3-Tier Distributed Lineage
        Assert.Equal(rootTraceId, consumerActivity.TraceId.ToHexString());
        Assert.Equal(outboxSpanId, consumerActivity.ParentSpanId.ToHexString());
        var consumerSpanId = consumerActivity.SpanId.ToHexString();

        // All 3 spans must have distinct span IDs
        Assert.NotEqual(rootSpanId, outboxSpanId);
        Assert.NotEqual(outboxSpanId, consumerSpanId);
        Assert.NotEqual(rootSpanId, consumerSpanId);
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
        services.AddScoped<ProcessMaintenanceCompletedUseCase>();

        return services.BuildServiceProvider();
    }

    private sealed class SpyRabbitMqPublisher : IRabbitMqPublisher
    {
        public record Call(
            Guid MessageId,
            string EventType,
            string RoutingKey,
            string Payload,
            string? TraceParent);

        public List<Call> PublishedCalls { get; } = new();

        public Task PublishAsync(
            Guid messageId,
            string eventType,
            string routingKey,
            string payload,
            CancellationToken cancellationToken = default)
        {
            return PublishAsync(messageId, eventType, routingKey, payload, null, cancellationToken);
        }

        public Task PublishAsync(
            Guid messageId,
            string eventType,
            string routingKey,
            string payload,
            string? traceParent,
            CancellationToken cancellationToken = default)
        {
            PublishedCalls.Add(new Call(messageId, eventType, routingKey, payload, traceParent));
            return Task.CompletedTask;
        }
    }
}
