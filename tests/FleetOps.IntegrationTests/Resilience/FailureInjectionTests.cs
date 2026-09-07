namespace FleetOps.IntegrationTests.Resilience;

using System.Text;
using System.Text.Json;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.Exceptions;
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
using FleetOps.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

public sealed class FailureInjectionTests : BaseIntegrationTest
{
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly RabbitMqConnection _connection;
    private readonly RabbitMqPublisher _publisher;

    public FailureInjectionTests()
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
    public async Task PublishSuccess_DatabaseUpdateFails_RedeliveryHandledIdempotentlyByConsumer()
    {
        // =========================================================================
        // FAILURE SCENARIO:
        // Outbox worker publishes event to RabbitMQ successfully under Publisher Confirms,
        // but crashes before marking processed_on_utc in PostgreSQL.
        // Worker restarts, retries, and republishes the same message (duplicate publication).
        // Consumer receives duplicate deliveries.
        // INVARIANT: Business effect executes exactly once.
        // =========================================================================

        var messageId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(750m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // 1. Initial publication (Simulates first worker attempt)
        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload);

        // 2. Simulated republishing due to worker crash before DB update (duplicate publication)
        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload);

        // 3. Start Consumer to process all delivered messages from the queue
        var services = BuildServiceProvider();
        var consumer = new MaintenanceCompletedConsumer(
            _connection,
            services.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(_rabbitOptions),
            NullLogger<MaintenanceCompletedConsumer>.Instance);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await consumer.StartAsync(cts.Token);

        // Await consumption of both messages (first inserts, second dedupes)
        for (var i = 0; i < 50; i++)
        {
            await Task.Delay(100);
            await using var context = CreateDbContext();
            var recordCount = await context.MaintenanceCompletionRecords
                .CountAsync(r => r.MessageId == messageId);

            if (recordCount > 0)
            {
                // Give a short window for duplicate message processing
                await Task.Delay(300);
                break;
            }
        }

        await consumer.StopAsync(CancellationToken.None);

        // 4. Assert: Invariant holds - exactly one business record created
        await using (var context = CreateDbContext())
        {
            var records = await context.MaintenanceCompletionRecords
                .Where(r => r.MessageId == messageId)
                .ToListAsync();

            Assert.Single(records);
            Assert.Equal(messageId, records[0].MessageId);
            Assert.Equal(maintenanceId, records[0].MaintenanceId);
            Assert.Equal(vehicleId, records[0].VehicleId);
        }

        // 5. Assert: Both deliveries were acknowledged; queue is completely empty
        await using var channel = await _connection.CreateChannelAsync();
        var remaining = await channel.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task ConsumerCrash_BeforeAck_RabbitMqRedeliveryMaintainsSingleBusinessEffect()
    {
        // =========================================================================
        // FAILURE SCENARIO:
        // Consumer pulls message from RabbitMQ, executes business logic, and commits
        // to PostgreSQL successfully.
        // However, before channel.BasicAckAsync reaches RabbitMQ, the connection/channel crashes.
        // RabbitMQ redelivers the unacknowledged message with redelivered=true.
        // Consumer receives redelivered message, detects existing record, and ACKs.
        // INVARIANT: Business effect executed only once, 0 duplicate records.
        // =========================================================================

        var messageId = Guid.NewGuid();
        var maintenanceId = Guid.NewGuid();
        var vehicleId = Guid.NewGuid();
        var completedAt = DateTimeOffset.UtcNow;

        var domainEvent = new MaintenanceCompletedDomainEvent(
            maintenanceId,
            vehicleId,
            new Money(1500m, "USD"),
            completedAt,
            DateTimeOffset.UtcNow);

        var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        // Publish message to queue
        await _publisher.PublishAsync(
            messageId,
            "MaintenanceCompleted",
            "maintenance.completed",
            payload);

        // 1. First consumer step: Read message manually without sending ACK
        var services = BuildServiceProvider();
        var channel1 = await _connection.CreateChannelAsync();
        var msg = await channel1.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: false);
        Assert.NotNull(msg);
        Assert.False(msg.Redelivered);

        // Execute business operation and commit transaction into database
        using (var scope = services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessMaintenanceCompletedUseCase>();
            var command = new ProcessMaintenanceCompletedCommand(
                messageId,
                maintenanceId,
                vehicleId,
                completedAt);
            await useCase.ExecuteAsync(command, CancellationToken.None);
        }

        // CRASH SIMULATION: Close channel1 abruptly WITHOUT sending ACK
        await channel1.CloseAsync();
        await channel1.DisposeAsync();

        // 2. RabbitMQ redelivers message because it was never ACKed.
        // Verify message is available again with Redelivered == true
        await using var channel2 = await _connection.CreateChannelAsync();
        var redeliveredMsg = await channel2.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: false);
        Assert.NotNull(redeliveredMsg);
        Assert.True(redeliveredMsg.Redelivered);

        // 3. Second consumer step: Process redelivered message through use case
        using (var scope = services.CreateScope())
        {
            var useCase = scope.ServiceProvider.GetRequiredService<ProcessMaintenanceCompletedUseCase>();
            var command = new ProcessMaintenanceCompletedCommand(
                messageId,
                maintenanceId,
                vehicleId,
                completedAt);

            // Idempotency check detects existing record and returns safely
            await useCase.ExecuteAsync(command, CancellationToken.None);
        }

        // Send ACK on channel2 to complete recovery
        await channel2.BasicAckAsync(redeliveredMsg.DeliveryTag, multiple: false);

        // 4. Assert: Invariant holds - exactly 1 record in database
        await using (var context = CreateDbContext())
        {
            var count = await context.MaintenanceCompletionRecords
                .CountAsync(r => r.MessageId == messageId);
            Assert.Equal(1, count);
        }

        // 5. Assert: Queue is now empty
        var remaining = await channel2.BasicGetAsync(_rabbitOptions.MaintenanceQueueName, autoAck: true);
        Assert.Null(remaining);
    }

    [Fact]
    public async Task DatabaseTransactionRollback_RetainsInMemoryDomainEventsAndLeavesOutboxEmpty()
    {
        // =========================================================================
        // FAILURE SCENARIO:
        // Domain entity raises events and attempts to persist in DbContext.
        // A database constraint violation occurs during SaveChangesAsync, triggering rollback.
        // INVARIANT:
        // 1. Transaction rolls back cleanly; Outbox table has 0 rows.
        // 2. Aggregate in-memory domain events are NOT cleared (retained for retry/inspection).
        // =========================================================================

        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId,
            LicensePlate.Create("FLR-0001"),
            VehicleType.Van,
            "Renault",
            "Master",
            2022,
            1000,
            1500m);

        // Raise domain event by updating mileage
        vehicle.UpdateMileage(1200);
        Assert.Single(vehicle.DomainEvents);

        // Pre-insert a driver with conflicting license number to induce physical constraint failure
        var conflictingLicenseNumber = "CNH-FAIL-999";
        var driver1 = Driver.Create(Guid.NewGuid(), "Driver A", conflictingLicenseNumber, "da@fail.com", "+55110001");

        await using (var context = CreateDbContext())
        {
            var driverRepo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);
            await driverRepo.AddAsync(driver1);
            await uow.SaveChangesAsync();
        }

        // Attempt transaction saving vehicle AND a duplicate driver with the same license number
        var driver2 = Driver.Create(Guid.NewGuid(), "Driver B", conflictingLicenseNumber, "db@fail.com", "+55110002");

        var exceptionOccurred = false;
        await using (var context = CreateDbContext())
        {
            var vehicleRepo = new VehicleRepository(context);
            var driverRepo = new DriverRepository(context);
            var uow = CreateUnitOfWork(context);

            await vehicleRepo.AddAsync(vehicle);
            await driverRepo.AddAsync(driver2);

            try
            {
                await uow.SaveChangesAsync();
            }
            catch (ConflictException)
            {
                exceptionOccurred = true;
            }
        }

        // 1. Assert: Physical constraint violation was caught
        Assert.True(exceptionOccurred);

        // 2. Assert: In-memory domain events are retained because commit failed
        Assert.Single(vehicle.DomainEvents);
        Assert.IsType<VehicleMileageUpdatedDomainEvent>(vehicle.DomainEvents.First());

        // 3. Assert: Database rolled back - vehicle does NOT exist and Outbox contains ZERO messages
        await using (var context = CreateDbContext())
        {
            var persistedVehicle = await context.Vehicles.FindAsync(vehicleId);
            Assert.Null(persistedVehicle);

            var outboxMessages = await context.OutboxMessages
                .Where(m => m.Payload.Contains(vehicleId.ToString()))
                .ToListAsync();
            Assert.Empty(outboxMessages);
        }
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
