namespace FleetOps.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class FleetOpsDbContextFactory : IDesignTimeDbContextFactory<FleetOpsDbContext>
{
    public FleetOpsDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Port=5432;Database=fleetops;Username=postgres;Password=postgres;";

        var optionsBuilder = new DbContextOptionsBuilder<FleetOpsDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new FleetOpsDbContext(optionsBuilder.Options);
    }
}
