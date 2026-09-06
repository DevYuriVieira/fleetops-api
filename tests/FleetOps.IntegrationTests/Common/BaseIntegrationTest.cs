using FleetOps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace FleetOps.IntegrationTests.Common;

public abstract class BaseIntegrationTest : IAsyncLifetime
{
    protected static readonly string ConnectionString =
        Environment.GetEnvironmentVariable("FLEETOPS_TEST_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5433;Database=fleetops_test;Username=postgres;Password=;";

    private readonly DbContextOptions<FleetOpsDbContext> _options;

    protected BaseIntegrationTest()
    {
        _options = new DbContextOptionsBuilder<FleetOpsDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
    }

    protected FleetOpsDbContext CreateDbContext()
    {
        return new FleetOpsDbContext(_options);
    }

    protected UnitOfWork CreateUnitOfWork(FleetOpsDbContext context)
    {
        return new UnitOfWork(context);
    }

    public virtual async Task InitializeAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
        await ClearDatabaseAsync();
    }

    public virtual async Task DisposeAsync()
    {
        await ClearDatabaseAsync();
    }

    protected async Task ClearDatabaseAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE deliveries, maintenances, routes, vehicles, drivers, outbox_messages, processed_messages, maintenance_completion_records CASCADE;");
    }
}
