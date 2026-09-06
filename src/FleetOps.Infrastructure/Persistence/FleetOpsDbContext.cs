namespace FleetOps.Infrastructure.Persistence;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Primitives;
using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public class FleetOpsDbContext : DbContext
{
    public FleetOpsDbContext(DbContextOptions<FleetOpsDbContext> options)
        : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Driver> Drivers => Set<Driver>();
    public DbSet<Maintenance> Maintenances => Set<Maintenance>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FleetOpsDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var aggregatesWithEvents = ChangeTracker.Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (aggregatesWithEvents.Count > 0)
        {
            var outboxMessages = new List<OutboxMessage>();

            foreach (var aggregate in aggregatesWithEvents)
            {
                foreach (var domainEvent in aggregate.DomainEvents)
                {
                    outboxMessages.Add(OutboxMessage.FromDomainEvent(domainEvent));
                }
            }

            if (outboxMessages.Count > 0)
            {
                await OutboxMessages.AddRangeAsync(outboxMessages, cancellationToken);
            }
        }

        var result = await base.SaveChangesAsync(cancellationToken);

        // Events are cleared in-memory only after successful persistence commit
        foreach (var aggregate in aggregatesWithEvents)
        {
            aggregate.ClearDomainEvents();
        }

        return result;
    }
}
