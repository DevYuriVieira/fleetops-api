namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Domain.Entities;
using FleetOps.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(v => v.LicensePlate)
            .HasConversion(lp => lp.Value, v => LicensePlate.Create(v))
            .HasMaxLength(10)
            .HasColumnName("license_plate")
            .IsRequired();

        builder.Property(v => v.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(v => v.Make)
            .HasMaxLength(100)
            .HasColumnName("make")
            .IsRequired();

        builder.Property(v => v.Model)
            .HasMaxLength(100)
            .HasColumnName("model")
            .IsRequired();

        builder.Property(v => v.Year)
            .HasColumnName("year")
            .IsRequired();

        builder.Property(v => v.Mileage)
            .HasColumnName("mileage")
            .IsRequired();

        builder.Property(v => v.CapacityKg)
            .HasPrecision(10, 2)
            .HasColumnName("capacity_kg")
            .IsRequired();

        builder.Property(v => v.CurrentDriverId)
            .HasColumnName("current_driver_id")
            .IsRequired(false);

        builder.Property(v => v.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(v => v.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.HasIndex(v => v.LicensePlate)
            .IsUnique()
            .HasDatabaseName("uq_vehicles_license_plate");

        builder.HasOne<Driver>()
            .WithMany()
            .HasForeignKey(v => v.CurrentDriverId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(v => v.DomainEvents);
    }
}
