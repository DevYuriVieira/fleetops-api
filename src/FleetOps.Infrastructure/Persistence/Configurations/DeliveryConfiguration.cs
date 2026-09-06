namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Domain.Entities;
using FleetOps.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DeliveryConfiguration : IEntityTypeConfiguration<Delivery>
{
    public void Configure(EntityTypeBuilder<Delivery> builder)
    {
        builder.ToTable("deliveries");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.TrackingCode)
            .HasConversion(tc => tc.Value, v => TrackingCode.Create(v))
            .HasMaxLength(32)
            .HasColumnName("tracking_code")
            .IsRequired();

        builder.OwnsOne(d => d.Origin, ob =>
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

        builder.OwnsOne(d => d.Destination, db =>
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

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(d => d.Priority)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("priority")
            .IsRequired();

        builder.Property(d => d.WeightKg)
            .HasPrecision(10, 2)
            .HasColumnName("weight_kg")
            .IsRequired();

        builder.Property(d => d.AssignedVehicleId)
            .HasColumnName("assigned_vehicle_id")
            .IsRequired(false);

        builder.Property(d => d.AssignedDriverId)
            .HasColumnName("assigned_driver_id")
            .IsRequired(false);

        builder.Property(d => d.EstimatedDeliveryTime)
            .HasColumnName("estimated_delivery_time")
            .IsRequired(false);

        builder.Property(d => d.ActualDeliveryTime)
            .HasColumnName("actual_delivery_time")
            .IsRequired(false);

        builder.Property(d => d.CancellationReason)
            .HasMaxLength(500)
            .HasColumnName("cancellation_reason")
            .IsRequired(false);

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.HasIndex(d => d.TrackingCode)
            .IsUnique()
            .HasDatabaseName("uq_deliveries_tracking_code");

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(d => d.AssignedVehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(d => d.AssignedDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(d => d.AssignedVehicleId)
            .HasDatabaseName("ix_deliveries_assigned_vehicle_id");

        builder.HasIndex(d => d.AssignedDriverId)
            .HasDatabaseName("ix_deliveries_assigned_driver_id");

        builder.HasIndex(d => d.Status)
            .HasDatabaseName("ix_deliveries_status");

        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(d => d.DomainEvents);
    }
}
