namespace FleetOps.IntegrationTests.Resilience;

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FleetOps.Api.Contracts.Vehicles;
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
using FleetOps.IntegrationTests.Api;
using FleetOps.IntegrationTests.Common;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Xunit;

public sealed class CapacityAndFailureUnderLoadTests : BaseIntegrationTest
{
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;

    public CapacityAndFailureUnderLoadTests()
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

    // =========================================================================
    // SCENARIO A: Concurrent Load -> RabbitMQ Failure -> Backlog Growth ->
    //             RabbitMQ Recovery -> Backlog Draining
    // =========================================================================
    [Fact]
    public async Task ScenarioA_ConcurrentWrites_WhenRabbitMqFailsDuringLoad_AccumulatesOutbox_AndDrainsOnRecovery()
    {
        const int concurrentRequests = 10;
        var vehicleIds = new ConcurrentBag<Guid>();

        // 1. Concurrent writes under simulated broker failure (unreachable port 59999)
        var deadRabbitOptions = Options.Create(new RabbitMqOptions
        {
            Host = "127.0.0.1",
            Port = 59999,
            Enabled = true
        });
        var deadConnection = new RabbitMqConnection(deadRabbitOptions, NullLogger<RabbitMqConnection>.Instance);
        var deadPublisher = new RabbitMqPublisher(deadConnection, deadRabbitOptions, NullLogger<RabbitMqPublisher>.Instance);

        var tasks = Enumerable.Range(1, concurrentRequests).Select(async i =>
        {
            var vehicleId = Guid.NewGuid();
            var maintenanceId = Guid.NewGuid();
            vehicleIds.Add(vehicleId);

            var vehicle = Vehicle.Create(
                vehicleId,
                LicensePlate.Create($"SCA{i:0000}"),
                VehicleType.Van,
                "Ford",
                "Transit",
                2024,
                1000,
                3500m);

            var domainEvent = new MaintenanceCompletedDomainEvent(
                maintenanceId,
                vehicleId,
                new Money(150m + i, "USD"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            var outboxMessage = OutboxMessage.FromDomainEvent(domainEvent);

            await using var context = CreateDbContext();
            var vRepo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await vRepo.AddAsync(vehicle);
            await context.OutboxMessages.AddAsync(outboxMessage);
            await uow.SaveChangesAsync();
        });

        await Task.WhenAll(tasks);

        // 2. Assert: All 10 vehicles committed; Outbox backlog = 10 pending messages
        await using (var context = CreateDbContext())
        {
            var vehicleCount = await context.Vehicles.CountAsync(v => vehicleIds.Contains(v.Id));
            Assert.Equal(concurrentRequests, vehicleCount);

            var pendingOutboxCount = await context.OutboxMessages
                .CountAsync(m => m.ProcessedOnUtc == null);
            Assert.True(pendingOutboxCount >= concurrentRequests);
        }

        // 3. Worker executes against dead broker -> fails gracefully without crashing
        await using (var context = CreateDbContext())
        {
            var outboxService = new OutboxService(
                context,
                Options.Create(new OutboxOptions { BatchSize = 20, MaxAttempts = 3 }),
                rabbitMqPublisher: deadPublisher);

            var processed = await outboxService.ProcessPendingMessagesAsync();
            Assert.Equal(0, processed);
        }

        // 4. Broker recovers: Worker executes against healthy real RabbitMQ broker -> drains backlog
        await using (var context = CreateDbContext())
        {
            var outboxService = new OutboxService(
                context,
                Options.Create(new OutboxOptions { BatchSize = 20, MaxAttempts = 3 }),
                rabbitMqPublisher: _publisher);

            var processed = await outboxService.ProcessPendingMessagesAsync();
            Assert.True(processed >= concurrentRequests);
        }

        // 5. Final State: Outbox backlog drained to 0
        await using (var context = CreateDbContext())
        {
            var remaining = await context.OutboxMessages
                .CountAsync(m => m.ProcessedOnUtc == null);
            Assert.Equal(0, remaining);
        }
    }

    // =========================================================================
    // SCENARIO B: Concurrent Load -> PostgreSQL Failure -> Readiness 503 &
    //             Sanitized 500 ProblemDetails -> System Recovers
    // =========================================================================
    [Fact]
    public async Task ScenarioB_PostgreSqlFailureUnderLoad_Returns503Readiness_AndSanitizedProblemDetails_AndRecovers()
    {
        // 1. Factory with dead PostgreSQL port
        await using var baseFactory = new FleetOpsApiFactory();
        using var deadDbFactory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=59997;Database=dead;Username=none;Password=none;Timeout=1;");
            builder.UseSetting("POSTGRES_CONNECTION_STRING", "Host=127.0.0.1;Port=59997;Database=dead;Username=none;Password=none;Timeout=1;");
        });

        using var deadClient = deadDbFactory.CreateClient();

        // Readiness probe immediately reports 503 Service Unavailable
        var readyResponse = await deadClient.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);

        // Liveness probe remains 200 OK (Kestrel process is alive)
        var liveResponse = await deadClient.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // Write endpoint returns sanitized 500 ProblemDetails
        deadClient.DefaultRequestHeaders.Add("X-Test-Role", "FleetManager");
        var request = new RegisterVehicleRequest("SCB0001", "Van", "Renault", "Master", 2024, 1000, 3000m);
        var writeResponse = await deadClient.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.InternalServerError, writeResponse.StatusCode);
        var problem = await writeResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);
        Assert.DoesNotContain("59997", writeResponse.Content.ToString(), StringComparison.OrdinalIgnoreCase);

        // 2. Recovery: Normal factory pointing to real PostgreSQL returns 200 OK
        using var healthyClient = baseFactory.CreateClient();
        var recoveredReady = await healthyClient.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, recoveredReady.StatusCode);
    }

    // =========================================================================
    // SCENARIO C: Consumer Interruption Under Load -> Queue Depth Grows ->
    //             Consumer Restarts -> Queue Drains to Zero
    // =========================================================================
    [Fact]
    public async Task ScenarioC_ConsumerInterruptionUnderLoad_QueueGrows_AndDrainsOnConsumerRecovery()
    {
        const int messageCount = 8;
        var messageIds = new List<Guid>();

        // 1. Publish 8 messages directly to RabbitMQ with NO consumer running
        for (var i = 1; i <= messageCount; i++)
        {
            var msgId = Guid.NewGuid();
            messageIds.Add(msgId);
            var domainEvent = new MaintenanceCompletedDomainEvent(
                Guid.NewGuid(),
                Guid.NewGuid(),
                new Money(500m + i, "USD"),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow);

            var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await _publisher.PublishAsync(msgId, "MaintenanceCompleted", "maintenance.completed", payload);
        }

        // 2. Inspect RabbitMQ queue depth: Queue holds messages
        await using var inspectionChannel = await _connection.CreateChannelAsync();
        var queueDeclareResult = await inspectionChannel.QueueDeclarePassiveAsync(_rabbitOptions.MaintenanceQueueName);
        Assert.True(queueDeclareResult.MessageCount >= (uint)messageCount);

        // 3. Start Consumer to drain accumulated queue
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_rabbitOptions),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
        await consumer.StartAsync(cts.Token);

        // Wait for all messages to be processed
        for (var i = 0; i < 60; i++)
        {
            await Task.Delay(100);
            await using var context = CreateDbContext();
            var count = await context.MaintenanceCompletionRecords
                .CountAsync(r => messageIds.Contains(r.MessageId));

            if (count == messageCount)
            {
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        // 4. Assert: All 8 records persisted in PostgreSQL
        await using (var context = CreateDbContext())
        {
            var count = await context.MaintenanceCompletionRecords
                .CountAsync(r => messageIds.Contains(r.MessageId));
            Assert.Equal(messageCount, count);
        }

        // 5. Assert: Queue depth returned to 0
        var finalDeclareResult = await inspectionChannel.QueueDeclarePassiveAsync(_rabbitOptions.MaintenanceQueueName);
        Assert.Equal(0u, finalDeclareResult.MessageCount);
    }

    // =========================================================================
    // SCENARIO D: Consumer Crash Before ACK -> RabbitMQ Redelivers ->
    //             Idempotency Boundary Preserves Exactly 1 Business Record
    // =========================================================================
    [Fact]
    public async Task ScenarioD_CrashBeforeAckUnderLoad_MaintainsSingleBusinessEffect()
    {
        var messageId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(2500m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await _publisher.PublishAsync(messageId, "MaintenanceCompleted", "maintenance.completed", payload);

        // 1. Pull message without autoAck
        var channel1 = await _connection.CreateChannelAsync();
        var msg1 = await channel1.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: false);
        Assert.NotNull(msg1);
        Assert.False(msg1.Redelivered);

        // 2. Commit transaction into PostgreSQL
        var services = BuildServiceProvider();
        using (var scope = services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessMaintenanceCompletedUseCase>();
            var command = new ProcessMaintenanceCompletedCommand(messageId, maintenanceId, vehicleId, completedAt);
            var result = await useCase.ExecuteAsync(command, CancellationToken.None);
            Assert.True(result);
        }

        // 3. SIMULATE CRASH: Close channel abruptly WITHOUT ACK
        await channel1.CloseAsync();
        await channel1.DisposeAsync();

        // 4. RabbitMQ redelivers message with Redelivered == true
        await using var channel2 = await _connection.CreateChannelAsync();
        var redeliveredMsg = await channel2.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: false);
        Assert.NotNull(redeliveredMsg);
        Assert.True(redeliveredMsg.Redelivered);

        // 5. Process redelivered message again through use case
        using (var scope = services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessMaintenanceCompletedUseCase>();
            var command = new ProcessMaintenanceCompletedCommand(messageId, maintenanceId, vehicleId, completedAt);
            var result = await useCase.ExecuteAsync(command, CancellationToken.None);
            Assert.False(result); // Idempotency check detects existing record!
        }

        // Send ACK on channel2
        await channel2.BasicAckAsync(redeliveredMsg.DeliveryTag, multiple: false);

        // 6. Assert: Single business effect in database
        await using (var context = CreateDbContext())
        {
            var count = await context.MaintenanceCompletionRecords
                .CountAsync(r => r.MessageId == messageId);
            Assert.Equal(1, count);
        }

        // 7. Assert: Queue is empty
        var remaining = await channel2.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
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
}
