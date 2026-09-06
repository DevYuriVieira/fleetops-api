namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MaintenanceConfiguration : IEntityTypeConfiguration<Maintenance>
{
    public void Configure(EntityTypeBuilder<Maintenance> builder)
    {
        builder.ToTable("maintenances");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.VehicleId)
            .HasColumnName("vehicle_id")
            .IsRequired();

        builder.Property(m => m.Type)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("type")
            .IsRequired();

        builder.Property(m => m.Description)
            .HasMaxLength(500)
            .HasColumnName("description")
            .IsRequired();

        builder.Property(m => m.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(m => m.ScheduledAt)
            .HasColumnName("scheduled_at")
            .IsRequired();

        builder.Property(m => m.StartedAt)
            .HasColumnName("started_at")
            .IsRequired(false);

        builder.Property(m => m.CompletedAt)
            .HasColumnName("completed_at")
            .IsRequired(false);

        builder.OwnsOne(m => m.Cost, cb =>
        {
            cb.Property(c => c.Amount)
                .HasPrecision(12, 2)
                .HasColumnName("cost_amount");

            cb.Property(c => c.Currency)
                .HasMaxLength(3)
                .HasColumnName("cost_currency");
        });

        builder.Property(m => m.CancellationReason)
            .HasMaxLength(500)
            .HasColumnName("cancellation_reason")
            .IsRequired(false);

        builder.Property(m => m.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(m => m.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.HasOne<Vehicle>()
            .WithMany()
            .HasForeignKey(m => m.VehicleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Crucial partial unique index guaranteeing only one active maintenance per vehicle
        builder.HasIndex(m => m.VehicleId)
            .IsUnique()
            .HasDatabaseName("ix_maintenances_vehicle_id")
            .HasFilter("status IN ('Scheduled', 'InProgress')");

        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(m => m.DomainEvents);
    }
}
