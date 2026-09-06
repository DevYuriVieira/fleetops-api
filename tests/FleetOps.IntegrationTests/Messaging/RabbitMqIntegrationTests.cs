namespace FleetOps.IntegrationTests.Messaging;

using System.Text;
using System.Text.Json;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.UseCases.Maintenance;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Configuration;
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

public sealed class RabbitMqIntegrationTests : BaseIntegrationTest
{
    private readonly RabbitMqOptions _options;
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;

    public RabbitMqIntegrationTests()
    {
        _options = new RabbitMqOptions
        {
            Host = "127.0.0.1",
            Port = 5672,
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

        var optionsWrapper = Options.Create(_options);
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
            await channel.QueuePurgeAsync(_options.MaintenanceQueueName);
            await channel.QueuePurgeAsync(_options.MaintenanceDlqName);
            for (var i = 0; i < _options.RetryDelaysMilliseconds.Length; i++)
            {
                await channel.QueuePurgeAsync(_options.GetRetryQueueName(i));
            }
        }
        catch
        {
        }
    }

    [Fact]
    public async Task MaintenanceCompleted_CreatesOutboxMessage()
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("RAB-0001"),
            VehicleType.Truck,
            "Volvo",
            "FH",
            2023,
            10000,
            25000m);
        vehicle.SendToMaintenance();

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            MaintenanceType.Preventive,
            "Oil change",
            DateTimeOffset.UtcNow.AddDays(1));
        maintenance.Start(DateTimeOffset.UtcNow.AddHours(-1));
        maintenance.Complete(DateTimeOffset.UtcNow, new Money(500m, "USD"));

        await using (var context = CreateDbContext())
        {
            await context.Vehicles.AddAsync(vehicle);
            await context.Maintenances.AddAsync(maintenance);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateDbContext())
        {
            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "MaintenanceCompleted");

            Assert.NotNull(outboxMessage);
            Assert.Null(outboxMessage.ProcessedOnUtc);
            Assert.Equal(0, outboxMessage.Attempts);
            Assert.Contains(maintenance.Id.ToString(), outboxMessage.Payload);
        }
    }

    [Fact]
    public async Task OutboxProcessor_PublishesEventToRabbitMQ_WithPublisherConfirm()
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("RAB-0002"),
            VehicleType.Truck,
            "Scania",
            "R500",
            2023,
            15000,
            28000m);
        vehicle.SendToMaintenance();

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            MaintenanceType.Preventive,
            "Filter replacement",
            DateTimeOffset.UtcNow.AddDays(1));
        maintenance.Start(DateTimeOffset.UtcNow.AddHours(-2));
        maintenance.Complete(DateTimeOffset.UtcNow, new Money(350m, "USD"));

        await using (var context = CreateDbContext())
        {
            await context.Vehicles.AddAsync(vehicle);
            await context.Maintenances.AddAsync(maintenance);
            await context.SaveChangesAsync();
        }

        Guid outboxId;
        await using (var context = CreateDbContext())
        {
            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "MaintenanceCompleted");
            Assert.NotNull(outboxMessage);
            outboxId = outboxMessage.Id;
        }

        await using (var context = CreateDbContext())
        {
            var outboxOptions = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 });
            var outboxService = new OutboxService(context, outboxOptions, null, _publisher);

            var processed = await outboxService.ProcessPendingMessagesAsync();
            Assert.True(processed >= 1);
        }

        await using (var context = CreateDbContext())
        {
            var outboxMessage = await context.OutboxMessages.FindAsync(outboxId);
            Assert.NotNull(outboxMessage);
            Assert.NotNull(outboxMessage.ProcessedOnUtc);
            Assert.Equal(1, outboxMessage.Attempts);
            Assert.Null(outboxMessage.Error);
        }

        await using var channel = await _connection.CreateChannelAsync();
        var result = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.NotNull(result);
        Assert.Equal(outboxId.ToString(), result.BasicProperties.MessageId);
        var body = Encoding.UTF8.GetString(result.Body.ToArray());
        Assert.Contains(maintenance.Id.ToString(), body);
    }

    [Fact]
    public async Task Consumer_CreatesExactlyOne_MaintenanceCompletionRecord()
    {
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(1200m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload);

        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
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

        Assert.NotNull(record);
        Assert.Equal(messageId, record.MessageId);
        Assert.Equal(maintenanceId, record.MaintenanceId);
        Assert.Equal(vehicleId, record.VehicleId);

        await using var channel = await _connection.CreateChannelAsync();
        var remaining = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Consumer_WhenDuplicateMessageDelivered_ProducesOnlyOneRecord()
    {
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(400m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);
        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);

        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var context = CreateDbContext();
            var count = await context.MaintenanceCompletionRecords.CountAsync(r => r.MessageId == messageId);
            if (count == 1)
            {
                await using var checkChannel = await _connection.CreateChannelAsync();
                var queueInfo = await checkChannel.QueueDeclarePassiveAsync(_options.MaintenanceQueueName);
                if (queueInfo.MessageCount == 0)
                {
                    break;
                }
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        await using (var context = CreateDbContext())
        {
            var count = await context.MaintenanceCompletionRecords.CountAsync(r => r.MessageId == messageId);
            Assert.Equal(1, count);
        }

        await using var channel = await _connection.CreateChannelAsync();
        var remaining = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Consumer_WhenTransientFailureOccurs_FollowsRetrySchedule_AndReachesDLQ()
    {
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(750m, "USD"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);

        var failingRepo = new TransientFailingRepository();
        var services = BuildServiceProvider(failingRepo);
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        await consumer.StartAsync(cts.Token);

        BasicGetResult? dlqResult = null;
        for (var i = 0; i < 70; i++)
        {
            await Task.Delay(100);
            await using var checkChannel = await _connection.CreateChannelAsync();
            dlqResult = await checkChannel.BasicGetAsync(_options.MaintenanceDlqName, autoAck: true);
            if (dlqResult is not null)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        Assert.NotNull(dlqResult);
        Assert.Equal(messageId.ToString(), dlqResult.BasicProperties.MessageId);

        var headers = dlqResult.BasicProperties.Headers;
        Assert.NotNull(headers);
        Assert.True(headers.ContainsKey("x-retry-count"));
        Assert.True(headers.ContainsKey("x-dlq-reason"));

        await using var channel = await _connection.CreateChannelAsync();
        var remainingMain = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remainingMain);
    }

    [Fact]
    public async Task Consumer_WhenPermanentInvalidMessageReceived_RoutesToDLQ_WithoutRequeue()
    {
        var poisonPayload = "{ invalid json structure, missing tokens : ; }";
        var messageId = Guid.NewGuid();

        await using (var channel = await _connection.CreateChannelAsync())
        {
            var properties = new BasicProperties
            {
                MessageId = messageId.ToString(),
                Type = "MaintenanceCompleted",
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent
            };

            await channel.BasicPublishAsync(
                exchange: _options.ExchangeName,
                routingKey: "maintenance.completed",
                mandatory: true,
                basicProperties: properties,
                body: Encoding.UTF8.GetBytes(poisonPayload));
        }

        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        BasicGetResult? dlqResult = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var checkChannel = await _connection.CreateChannelAsync();
            dlqResult = await checkChannel.BasicGetAsync(_options.MaintenanceDlqName, autoAck: true);
            if (dlqResult is not null)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        Assert.NotNull(dlqResult);
        Assert.Equal(messageId.ToString(), dlqResult.BasicProperties.MessageId);
        var body = Encoding.UTF8.GetString(dlqResult.Body.ToArray());
        Assert.Equal(poisonPayload, body);

        await using var channelMain = await _connection.CreateChannelAsync();
        var remaining = await channelMain.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Api_WhenRabbitMqUnavailable_MaintenanceCompletionSucceeds_AndOutboxIsDurable()
    {
        var vehicle = Vehicle.Create(
            Guid.NewGuid(),
            LicensePlate.Create("RAB-0005"),
            VehicleType.Van,
            "Mercedes",
            "Sprinter",
            2022,
            40000,
            3500m);
        vehicle.SendToMaintenance();

        var maintenance = Maintenance.Create(
            Guid.NewGuid(),
            vehicle.Id,
            MaintenanceType.Preventive,
            "Brake system check",
            DateTimeOffset.UtcNow.AddDays(1));
        maintenance.Start(DateTimeOffset.UtcNow.AddHours(-1));

        await using (var context = CreateDbContext())
        {
            await context.Vehicles.AddAsync(vehicle);
            await context.Maintenances.AddAsync(maintenance);
            await context.SaveChangesAsync();
        }

        var command = new CompleteMaintenanceCommand(
            maintenance.Id,
            550m,
            "USD",
            DateTimeOffset.UtcNow,
            ReturnVehicleToActive: true);

        await using (var context = CreateDbContext())
        {
            var uow = CreateUnitOfWork(context);
            var mntRepo = new MaintenanceRepository(context);
            var vehRepo = new VehicleRepository(context);
            var useCase = new CompleteMaintenanceUseCase(mntRepo, vehRepo, uow);

            var result = await useCase.ExecuteAsync(command);
            Assert.Equal("Completed", result.Status);
        }

        await using (var context = CreateDbContext())
        {
            var persistedMnt = await context.Maintenances.FindAsync(maintenance.Id);
            Assert.NotNull(persistedMnt);
            Assert.Equal(MaintenanceStatus.Completed, persistedMnt.Status);

            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "MaintenanceCompleted");
            Assert.NotNull(outboxMessage);
            Assert.Null(outboxMessage.ProcessedOnUtc);
            Assert.Contains(maintenance.Id.ToString(), outboxMessage.Payload);
        }
    }

    [Fact]
    public async Task Outbox_WhenRabbitMqRecovers_PendingOutboxMessagesArePublished()
    {
        var vehicleId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var outboxMessage = new OutboxMessage(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "MaintenanceCompleted",
            JsonSerializer.Serialize(new MaintenanceCompletedDomainEvent(
                maintenanceId,
                vehicleId,
                new Money(300m, "USD"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow),
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

        outboxMessage.RecordFailure("Broker connection refused (simulated temporary outage)");

        await using (var context = CreateDbContext())
        {
            await context.OutboxMessages.AddAsync(outboxMessage);
            await context.SaveChangesAsync();
        }

        await using (var context = CreateDbContext())
        {
            var outboxOptions = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 });
            var outboxService = new OutboxService(context, outboxOptions, null, _publisher);

            var processed = await outboxService.ProcessPendingMessagesAsync();
            Assert.True(processed >= 1);
        }

        await using (var context = CreateDbContext())
        {
            var recovered = await context.OutboxMessages.FindAsync(outboxMessage.Id);
            Assert.NotNull(recovered);
            Assert.NotNull(recovered.ProcessedOnUtc);
            Assert.Null(recovered.Error);
        }

        await using var channel = await _connection.CreateChannelAsync();
        var result = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.NotNull(result);
        Assert.Equal(outboxMessage.Id.ToString(), result.BasicProperties.MessageId);
    }

    [Fact]
    public async Task Consumer_WhenCrashOccursAfterDbCommitButBeforeAck_RedeliveryDoesNotDuplicateRecord()
    {
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var messageId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        await using (var context = CreateDbContext())
        {
            var committedRecord = new MaintenanceCompletionRecord(
                messageId,
                maintenanceId,
                vehicleId,
                completedAt,
                DateTimeOffset.UtcNow);

            await context.MaintenanceCompletionRecords.AddAsync(committedRecord);
            await context.SaveChangesAsync();
        }

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(600m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);

        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        await Task.Delay(1000);
        await consumer.StopAsync(CancellationToken.None);

        await using (var context = CreateDbContext())
        {
            var count = await context.MaintenanceCompletionRecords.CountAsync(r => r.MessageId == messageId);
            Assert.Equal(1, count);
        }

        await using var channel = await _connection.CreateChannelAsync();
        var remaining = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Consumer_UsesPublisherConfirmations_ForRetryAndDlqPublishing()
    {
        var trackingConnection = new ChannelTrackingRabbitMqConnection(_connection);
        var failingRepo = new TransientFailingRepository();
        var services = BuildServiceProvider(failingRepo);
        var consumer = new MaintenanceCompletedConsumer(
            trackingConnection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        var messageId = Guid.NewGuid();
        var domainEvent = new MaintenanceCompletedDomainEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new Money(500m, "USD"),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        var retryQueueName = _options.GetRetryQueueName(0);
        BasicGetResult? retryResult = null;
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var checkChannel = await _connection.CreateChannelAsync();
            retryResult = await checkChannel.BasicGetAsync(retryQueueName, autoAck: true);
            if (retryResult is not null)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        var options = Assert.Single(trackingConnection.CapturedOptions);
        Assert.NotNull(options);
        Assert.True(options.PublisherConfirmationsEnabled);
        Assert.True(options.PublisherConfirmationTrackingEnabled);

        Assert.NotNull(retryResult);
        Assert.Equal(messageId.ToString(), retryResult.BasicProperties.MessageId);

        await using var channel = await _connection.CreateChannelAsync();
        var remaining = await channel.BasicGetAsync(_options.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task Consumer_WhenCancelledOrShutdown_TerminatesCleanly()
    {
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource();
        var task = consumer.StartAsync(cts.Token);

        await Task.Delay(200);
        cts.Cancel();

        await consumer.StopAsync(CancellationToken.None);
        Assert.True(task.IsCompleted);
    }

    [Fact]
    public async Task Consumer_WhenCancelledOrShutdown_DisposesChannelCleanly()
    {
        var trackingConnection = new ChannelTrackingRabbitMqConnection(_connection);
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            trackingConnection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_options),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource();
        var task = consumer.StartAsync(cts.Token);

        await Task.Delay(200);
        cts.Cancel();

        await consumer.StopAsync(CancellationToken.None);
        Assert.True(task.IsCompleted);

        var channel = Assert.Single(trackingConnection.CreatedChannels);
        Assert.True(channel.IsClosed);
        Assert.False(channel.IsOpen);
    }

    private sealed class ChannelTrackingRabbitMqConnection : IRabbitMqConnection
    {
        private readonly IRabbitMqConnection _inner;
        public List<CreateChannelOptions?> CapturedOptions { get; } = new();
        public List<IChannel> CreatedChannels { get; } = new();

        public ChannelTrackingRabbitMqConnection(IRabbitMqConnection inner)
        {
            _inner = inner;
        }

        public bool IsConnected => _inner.IsConnected;

        public Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default) =>
            _inner.GetConnectionAsync(cancellationToken);

        public async Task<IChannel> CreateChannelAsync(CreateChannelOptions? options = null, CancellationToken cancellationToken = default)
        {
            CapturedOptions.Add(options);
            var channel = await _inner.CreateChannelAsync(options, cancellationToken);
            CreatedChannels.Add(channel);
            return channel;
        }

        public Task InitializeTopologyAsync(CancellationToken cancellationToken = default) =>
            _inner.InitializeTopologyAsync(cancellationToken);

        public ValueTask DisposeAsync() => _inner.DisposeAsync();
    }

    private IServiceProvider BuildServiceProvider(IMaintenanceCompletionRecordRepository? repoOverride = null)
    {
        var services = new ServiceCollection();

        services.AddDbContext<FleetOpsDbContext>((_, options) =>
        {
            options.UseNpgsql(ConnectionString);
        });

        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IMaintenanceRepository, MaintenanceRepository>();
        if (repoOverride is not null)
        {
            services.AddScoped<IMaintenanceCompletionRecordRepository>(_ => repoOverride);
        }
        else
        {
            services.AddScoped<IMaintenanceCompletionRecordRepository, MaintenanceCompletionRecordRepository>();
        }
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ProcessMaintenanceCompletedUseCase>();

        return services.BuildServiceProvider();
    }

    private sealed class TransientFailingRepository : IMaintenanceCompletionRecordRepository
    {
        public Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("Database connection timed out (simulated transient network partition)");
        }

        public Task AddAsync(
            Guid messageId,
            Guid maintenanceId,
            Guid vehicleId,
            DateTimeOffset completedOnUtc,
            DateTimeOffset processedOnUtc,
            CancellationToken cancellationToken = default)
        {
            throw new TimeoutException("Database connection timed out (simulated transient network partition)");
        }
    }
}
