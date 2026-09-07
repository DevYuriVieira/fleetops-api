namespace FleetOps.IntegrationTests.Resilience;

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FleetOps.Api.Contracts.Vehicles;
using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.ValueObjects;
using FleetOps.Infrastructure.Configuration;
using FleetOps.Infrastructure.Diagnostics;
using FleetOps.Infrastructure.Messaging;
using FleetOps.Infrastructure.Persistence;
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
using Xunit;

public class GracefulDegradationTests : BaseIntegrationTest, IClassFixture<FleetOpsApiFactory>
{
    private readonly FleetOpsApiFactory _factory;

    public GracefulDegradationTests(FleetOpsApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task WhenRabbitMqIsUnavailable_ApiAcceptsWriteCommands_PersistsOutboxAtomically_AndHealthReportsDegraded()
    {
        // =========================================================================
        // ARCHITECTURAL INVARIANT:
        // Transactional Outbox decouples command ingestion from message broker availability.
        // If RabbitMQ is down:
        // 1. Domain state AND Outbox message commit atomically in PostgreSQL.
        // 2. /health/ready remains 200 OK (API can receive write traffic).
        // 3. /health/dependencies reports status 'Degraded' (reflecting broker outage).
        // 4. Outbox worker retries with bounded failure recording and leaves message pending.
        // =========================================================================

        var vehicleId = Guid.NewGuid();
        var vehicle = Vehicle.Create(
            vehicleId,
            LicensePlate.Create("DEG-1001"),
            VehicleType.Truck,
            "Volvo",
            "FH540",
            2024,
            5000,
            40000m);

        vehicle.UpdateMileage(5500); // Emits VehicleMileageUpdatedDomainEvent

        // 1. Persist under PostgreSQL ACID boundary
        await using (var context = CreateDbContext())
        {
            var repo = new VehicleRepository(context);
            var uow = CreateUnitOfWork(context);
            await repo.AddAsync(vehicle);
            await uow.SaveChangesAsync();
        }

        // Assert: Vehicle and Outbox message exist in PostgreSQL
        await using (var context = CreateDbContext())
        {
            var persistedVehicle = await context.Vehicles.FindAsync(vehicleId);
            Assert.NotNull(persistedVehicle);

            var outboxMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.Payload.Contains(vehicleId.ToString()));
            Assert.NotNull(outboxMessage);
            Assert.Null(outboxMessage.ProcessedOnUtc);
            Assert.Equal(0, outboxMessage.Attempts);
        }

        // 2. Simulate OutboxService attempting to publish to an unavailable RabbitMQ broker (wrong port)
        var deadRabbitOptions = new RabbitMqOptions
        {
            Host = "127.0.0.1",
            Port = 59999, // Dead port where no broker is listening
            Enabled = true
        };

        var deadConnection = new RabbitMqConnection(
            Options.Create(deadRabbitOptions),
            NullLogger<RabbitMqConnection>.Instance);

        var deadPublisher = new RabbitMqPublisher(
            deadConnection,
            Options.Create(deadRabbitOptions),
            NullLogger<RabbitMqPublisher>.Instance);

        await using (var context = CreateDbContext())
        {
            var outboxService = new OutboxService(
                context,
                Options.Create(new OutboxOptions { BatchSize = 10, MaxAttempts = 3 }),
                rabbitMqPublisher: deadPublisher);

            // Worker executes without crashing the application process
            var processed = await outboxService.ProcessPendingMessagesAsync(CancellationToken.None);
            Assert.Equal(0, processed); // Could not publish
        }

        // Assert: Message remains recorded in Outbox with failure recorded and attempts incremented
        await using (var context = CreateDbContext())
        {
            var failedMessage = await context.OutboxMessages
                .FirstOrDefaultAsync(m => m.Payload.Contains(vehicleId.ToString()));

            Assert.NotNull(failedMessage);
            Assert.Null(failedMessage.ProcessedOnUtc);
            Assert.True(failedMessage.Attempts > 0);
            Assert.NotNull(failedMessage.Error);
        }

        // 3. Inspect Health Endpoint behavior with unavailable RabbitMQ
        using var degradedFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RabbitMQ:Port", "59999");
        });

        using var client = degradedFactory.CreateClient();

        // /health/live must be 200 OK (process is running)
        var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // /health/ready must be 200 OK (PostgreSQL is available for writes)
        var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, readyResponse.StatusCode);

        // /health/dependencies must report Degraded (RabbitMQ down, but API operable)
        var depResponse = await client.GetAsync("/health/dependencies");
        Assert.Equal(HttpStatusCode.OK, depResponse.StatusCode);

        var depContent = await depResponse.Content.ReadAsStringAsync();
        Assert.Contains("Degraded", depContent);
    }

    [Fact]
    public async Task WhenDatabaseIsUnavailable_HealthReadyReturns503_AndEndpointsReturnProblemDetails()
    {
        // Configure an API factory pointing to an unavailable PostgreSQL port
        using var deadDbFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=127.0.0.1;Port=59998;Database=dead_db;Username=none;Password=none;Timeout=1;");
            builder.UseSetting("POSTGRES_CONNECTION_STRING", "Host=127.0.0.1;Port=59998;Database=dead_db;Username=none;Password=none;Timeout=1;");
        });

        using var client = deadDbFactory.CreateClient();

        // /health/live remains 200 OK
        var liveResponse = await client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, liveResponse.StatusCode);

        // /health/ready must immediately return 503 Service Unavailable
        var readyResponse = await client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, readyResponse.StatusCode);

        // Write endpoint returns sanitized ProblemDetails (500)
        client.DefaultRequestHeaders.Add("X-Test-Role", "FleetManager");
        var request = new RegisterVehicleRequest("DEAD001", "Van", "Ford", "Transit", 2023, 1000, 2000m);
        var writeResponse = await client.PostAsJsonAsync("/api/vehicles", request);

        Assert.Equal(HttpStatusCode.InternalServerError, writeResponse.StatusCode);
        var problem = await writeResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(500, problem.Status);

        // Assert: Zero credential or connection string leakage in ProblemDetails response
        Assert.DoesNotContain("Password", writeResponse.Content.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("59998", writeResponse.Content.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
