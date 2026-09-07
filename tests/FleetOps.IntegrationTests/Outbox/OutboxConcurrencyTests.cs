namespace FleetOps.IntegrationTests.Outbox;

using System.Collections.Concurrent;
using System.Text.Json;
using FleetOps.Application.Abstractions.Events;
using FleetOps.Domain.Events;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Persistence.Entities;
using FleetOps.Infrastructure.Services;
using FleetOps.IntegrationTests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

public class OutboxConcurrencyTests : BaseIntegrationTest
{
    private sealed class TrackingDispatcher : IDomainEventDispatcher
    {
        public ConcurrentBag<(string Worker, Guid VehicleId)> ProcessedEvents { get; } = new();
        private readonly string _workerName;
        private readonly int _simulateWorkMs;

        public TrackingDispatcher(string workerName, int simulateWorkMs = 20)
        {
            _workerName = workerName;
            _simulateWorkMs = simulateWorkMs;
        }

        public async Task DispatchEventsAsync(IEnumerable<IDomainEvent> events, CancellationToken cancellationToken = default)
        {
            if (_simulateWorkMs > 0)
            {
                await Task.Delay(_simulateWorkMs, cancellationToken);
            }

            foreach (var ev in events)
            {
                if (ev is VehicleMileageUpdatedDomainEvent mileageEvent)
                {
                    ProcessedEvents.Add((_workerName, mileageEvent.VehicleId));
                }
            }
        }
    }

    [Fact]
    public async Task ConcurrentReplicas_WithSkipLocked_ProcessDisjointMessagesWithoutContention()
    {
        // 1. Seed 20 outbox messages
        const int totalMessages = 20;
        var seededVehicleIds = new List<Guid>();

        await using (var seedContext = CreateDbContext())
        {
            for (var i = 1; i <= totalMessages; i++)
            {
                var vehicleId = Guid.NewGuid();
                seededVehicleIds.Add(vehicleId);

                var domainEvent = new VehicleMileageUpdatedDomainEvent(
                    vehicleId,
                    OldMileage: i * 100,
                    NewMileage: (i * 100) + 50,
                    OccurredOn: DateTimeOffset.UtcNow.AddMinutes(-totalMessages + i));

                var payload = JsonSerializer.Serialize(domainEvent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var message = new OutboxMessage(
                    Guid.NewGuid(),
                    domainEvent.OccurredOn,
                    "VehicleMileageUpdated",
                    payload);

                await seedContext.OutboxMessages.AddAsync(message);
            }

            await seedContext.SaveChangesAsync();
        }

        // 2. Prepare 2 concurrent OutboxService instances (representing Pod 1 and Pod 2)
        var dispatcher1 = new TrackingDispatcher("Replica-1", simulateWorkMs: 25);
        var dispatcher2 = new TrackingDispatcher("Replica-2", simulateWorkMs: 25);

        var options = Options.Create(new OutboxOptions
        {
            BatchSize = 10,
            MaxAttempts = 3
        });

        // 3. Concurrently run both workers with a synchronization barrier
        using var barrier = new Barrier(2);

        var task1 = Task.Run(async () =>
        {
            await using var context1 = CreateDbContext();
            var service1 = new OutboxService(context1, options, dispatcher1);
            barrier.SignalAndWait();
            return await service1.ProcessPendingMessagesAsync();
        });

        var task2 = Task.Run(async () =>
        {
            await using var context2 = CreateDbContext();
            var service2 = new OutboxService(context2, options, dispatcher2);
            barrier.SignalAndWait();
            return await service2.ProcessPendingMessagesAsync();
        });

        var results = await Task.WhenAll(task1, task2);
        var count1 = results[0];
        var count2 = results[1];

        // 4. Assert: Both workers processed distinct messages without overlap
        var worker1Ids = dispatcher1.ProcessedEvents.Select(e => e.VehicleId).ToList();
        var worker2Ids = dispatcher2.ProcessedEvents.Select(e => e.VehicleId).ToList();

        var intersection = worker1Ids.Intersect(worker2Ids).ToList();
        Assert.Empty(intersection); // ZERO duplicates!

        Assert.Equal(totalMessages, count1 + count2);
        Assert.Equal(10, count1);
        Assert.Equal(10, count2);

        // 5. Assert: In database, all 20 messages are marked processed with 1 attempt
        await using (var verifyContext = CreateDbContext())
        {
            var unprocessed = await verifyContext.OutboxMessages
                .CountAsync(m => m.ProcessedOnUtc == null);
            Assert.Equal(0, unprocessed);

            var processedMessages = await verifyContext.OutboxMessages
                .Where(m => m.ProcessedOnUtc != null)
                .ToListAsync();

            Assert.Equal(totalMessages, processedMessages.Count);
            Assert.All(processedMessages, m => Assert.Equal(1, m.Attempts));
            Assert.All(processedMessages, m => Assert.Null(m.Error));
        }
    }
}
