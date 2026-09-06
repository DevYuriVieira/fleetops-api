using FleetOps.Application.Abstractions.Events;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Persistence.Repositories;
using FleetOps.Infrastructure.Services;
using FleetOps.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace FleetOps.IntegrationTests.Outbox;

public class OutboxProcessorTests : BaseIntegrationTest
{
    private class TestEventDispatcher : IDomainEventDispatcher
    {
        public List<IDomainEvent> DispatchedEvents { get; } = new();
        public bool ShouldThrow { get; set; }

        public Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (ShouldThrow)
            {
                throw new InvalidOperationException("Simulated external dispatch failure");
            }

            DispatchedEvents.AddRange(events);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_ShouldDispatchEventsAndMarkOutboxMessagesProcessed()
    {
        // Arrange: create vehicle and assign driver to generate DriverAssignedToVehicleDomainEvent
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var driver = Driver.Create(driverId, "Driver 1", "DL000001", "d1@outbox.com", "+1001");
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("OUT1234"), VehicleType.Truck, "Volvo", "FH16", 2023, 1000, 50000m);
        vehicle.AssignDriver(driverId);

        await using (var context = CreateDbContext())
        {
            var dRepo = new DriverRepository(context);
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await dRepo.AddAsync(driver);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        var dispatcher = new TestEventDispatcher();
        var options = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 });

        // Act: Process outbox batch
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            var processedCount = await service.ProcessPendingMessagesAsync(CancellationToken.None);

            Assert.Equal(1, processedCount);
        }

        // Assert: Event was dispatched with valid type and data
        Assert.Single(dispatcher.DispatchedEvents);
        var dispatchedEvent = Assert.IsType<DriverAssignedToVehicleDomainEvent>(dispatcher.DispatchedEvents[0]);
        Assert.Equal(vehicleId, dispatchedEvent.VehicleId);
        Assert.Equal(driverId, dispatchedEvent.DriverId);

        // Assert: Outbox record in database updated
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "DriverAssignedToVehicle" && m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(msg);
            Assert.NotNull(msg.ProcessedOnUtc);
            Assert.Equal(1, msg.Attempts);
            Assert.Null(msg.Error);
        }
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_WhenDispatcherFails_RecordsFailureAndRetries()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var driver = Driver.Create(driverId, "Driver 2", "DL000002", "d2@outbox.com", "+1002");
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("OUT5678"), VehicleType.Truck, "MAN", "TGX", 2022, 2000, 40000m);
        vehicle.AssignDriver(driverId);

        await using (var context = CreateDbContext())
        {
            var dRepo = new DriverRepository(context);
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await dRepo.AddAsync(driver);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        var dispatcher = new TestEventDispatcher { ShouldThrow = true };
        var options = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 });

        // Act 1: First attempt fails
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            var processedCount = await service.ProcessPendingMessagesAsync(CancellationToken.None);

            Assert.Equal(0, processedCount);
        }

        // Assert 1: Attempts = 1, Error recorded, ProcessedOnUtc is null
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "DriverAssignedToVehicle" && m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(msg);
            Assert.Null(msg.ProcessedOnUtc);
            Assert.Equal(1, msg.Attempts);
            Assert.Contains("Simulated external dispatch failure", msg.Error);
        }

        // Act 2: Dispatcher recovers, retry succeeds
        dispatcher.ShouldThrow = false;
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            var processedCount = await service.ProcessPendingMessagesAsync(CancellationToken.None);

            Assert.Equal(1, processedCount);
        }

        // Assert 2: Processed successfully on retry
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "DriverAssignedToVehicle" && m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(msg);
            Assert.NotNull(msg.ProcessedOnUtc);
            Assert.Equal(2, msg.Attempts);
        }
    }

    [Fact]
    public async Task ProcessPendingMessagesAsync_ExceedingMaxAttempts_BecomesPoisonMessageAndIsSkipped()
    {
        // Arrange
        var vehicleId = Guid.NewGuid();
        var driverId = Guid.NewGuid();
        var driver = Driver.Create(driverId, "Driver 3", "DL000003", "d3@outbox.com", "+1003");
        var vehicle = Vehicle.Create(
            vehicleId, LicensePlate.Create("OUT9999"), VehicleType.Truck, "DAF", "XF", 2021, 1000, 30000m);
        vehicle.AssignDriver(driverId);

        await using (var context = CreateDbContext())
        {
            var dRepo = new DriverRepository(context);
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await dRepo.AddAsync(driver);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        var dispatcher = new TestEventDispatcher { ShouldThrow = true };
        var options = Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 2 });

        // Attempt 1 fails
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            await service.ProcessPendingMessagesAsync(CancellationToken.None);
        }

        // Attempt 2 fails (reaches MaxAttempts)
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            await service.ProcessPendingMessagesAsync(CancellationToken.None);
        }

        // Act: Attempt 3 - message has reached MaxAttempts (2), so it must NOT be selected for processing
        dispatcher.ShouldThrow = false;
        await using (var context = CreateDbContext())
        {
            var service = new OutboxService(context, options, dispatcher);
            var processedCount = await service.ProcessPendingMessagesAsync(CancellationToken.None);

            Assert.Equal(0, processedCount);
        }

        // Message remains uncommitted and unprocessed, flagged with poison error
        await using (var context = CreateDbContext())
        {
            var msg = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.EventType == "DriverAssignedToVehicle" && m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(msg);
            Assert.Null(msg.ProcessedOnUtc);
            Assert.Equal(2, msg.Attempts);
        }
    }

    [Fact]
    public async Task OutboxProcessor_WhenOutboxServiceThrows_LogsErrorAndDoesNotCrash()
    {
        var services = new ServiceCollection();
        var failingOutbox = new FailingOutboxService();
        services.AddScoped<IOutboxService>(_ => failingOutbox);
        var provider = services.BuildServiceProvider();

        var testLogger = new TestLogger<OutboxProcessor>();
        var options = Options.Create(new OutboxOptions { IntervalSeconds = 1, Enabled = true });

        var processor = new OutboxProcessor(provider.GetRequiredService<IServiceScopeFactory>(), options, testLogger);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        await processor.StartAsync(cts.Token);
        await Task.Delay(200);
        await processor.StopAsync(CancellationToken.None);

        Assert.Contains(testLogger.LoggedMessages, m =>
            m.LogLevel == LogLevel.Error &&
            m.Exception is InvalidOperationException &&
            m.Message.Contains("Outbox processor execution failed"));
    }

    private sealed class FailingOutboxService : IOutboxService
    {
        public Task<int> ProcessPendingMessagesAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated database failure during outbox processing");
        }
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<(LogLevel LogLevel, string Message, Exception? Exception)> LoggedMessages { get; } = new();

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LoggedMessages.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
