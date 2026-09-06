namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class RouteConfiguration : IEntityTypeConfiguration<Route>
{
    public void Configure(EntityTypeBuilder<Route> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.OwnsOne(r => r.Origin, ob =>
        {
            ob.Property(o => o.Street).HasMaxLength(200).HasColumnName("origin_street").IsRequired();
            ob.Property(o => o.Number).HasMaxLength(20).HasColumnName("origin_number").IsRequired();
            ob.Property(o => o.Neighborhood).HasMaxLength(100).HasColumnName("origin_neighborhood").IsRequired();
            ob.Property(o => o.City).HasMaxLength(100).HasColumnName("origin_city").IsRequired();
            ob.Property(o => o.State).HasMaxLength(50).HasColumnName("origin_state").IsRequired();
            ob.Property(o => o.PostalCode).HasMaxLength(20).HasColumnName("origin_postal_code").IsRequired();
            ob.Property(o => o.Country).HasMaxLength(50).HasColumnName("origin_country").IsRequired();
            ob.Property(o => o.Complement).HasMaxLength(100).HasColumnName("origin_complement").IsRequired(false);
        });

        builder.OwnsOne(r => r.Destination, db =>
        {
            db.Property(o => o.Street).HasMaxLength(200).HasColumnName("destination_street").IsRequired();
            db.Property(o => o.Number).HasMaxLength(20).HasColumnName("destination_number").IsRequired();
            db.Property(o => o.Neighborhood).HasMaxLength(100).HasColumnName("destination_neighborhood").IsRequired();
            db.Property(o => o.City).HasMaxLength(100).HasColumnName("destination_city").IsRequired();
            db.Property(o => o.State).HasMaxLength(50).HasColumnName("destination_state").IsRequired();
            db.Property(o => o.PostalCode).HasMaxLength(20).HasColumnName("destination_postal_code").IsRequired();
            db.Property(o => o.Country).HasMaxLength(50).HasColumnName("destination_country").IsRequired();
            db.Property(o => o.Complement).HasMaxLength(100).HasColumnName("destination_complement").IsRequired(false);
        });

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(r => r.PlannedDeparture)
            .HasColumnName("planned_departure")
            .IsRequired();

        builder.Property(r => r.ActualDeparture)
            .HasColumnName("actual_departure")
            .IsRequired(false);

        builder.Property(r => r.EstimatedArrival)
            .HasColumnName("estimated_arrival")
            .IsRequired();

        builder.Property(r => r.ActualArrival)
            .HasColumnName("actual_arrival")
            .IsRequired(false);

        builder.Property(r => r.AssignedVehicleId)
            .HasColumnName("assigned_vehicle_id")
            .IsRequired(false);

        builder.Property(r => r.AssignedDriverId)
            .HasColumnName("assigned_driver_id")
            .IsRequired(false);

        builder.Property(r => r.CancellationReason)
            .HasMaxLength(500)
            .HasColumnName("cancellation_reason")
            .IsRequired(false);

        builder.Property(r => r.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        var deliveryIdsComparer = new ValueComparer<IReadOnlyCollection<Guid>>(
            (c1, c2) => (c1 == null && c2 == null) || (c1 != null && c2 != null && c1.SequenceEqual(c2)),
            c => c.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
            c => c.ToList().AsReadOnly());

        builder.Property(r => r.DeliveryIds)
            .HasField("_deliveryIds")
            .HasColumnName("delivery_ids")
            .HasColumnType("uuid[]")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .Metadata.SetValueComparer(deliveryIdsComparer);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(r => r.AssignedVehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(r => r.AssignedDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.AssignedVehicleId)
            .HasDatabaseName("ix_routes_assigned_vehicle_id");

        builder.HasIndex(r => r.AssignedDriverId)
            .HasDatabaseName("ix_routes_assigned_driver_id");

        builder.HasIndex(r => r.Status)
            .HasDatabaseName("ix_routes_status");

        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(r => r.DomainEvents);
    }
}
