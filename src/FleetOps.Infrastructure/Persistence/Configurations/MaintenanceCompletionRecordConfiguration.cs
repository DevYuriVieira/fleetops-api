namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class MaintenanceCompletionRecordConfiguration : IEntityTypeConfiguration<MaintenanceCompletionRecord>
{
    public void Configure(EntityTypeBuilder<MaintenanceCompletionRecord> builder)
    {
        builder.ToTable("maintenance_completion_records");

        builder.HasKey(r => r.MessageId);

        builder.Property(r => r.MessageId)
            .HasColumnName("message_id");

        builder.Property(r => r.MaintenanceId)
            .HasColumnName("maintenance_id")
            .IsRequired();

        builder.Property(r => r.VehicleId)
            .HasColumnName("vehicle_id")
            .IsRequired();

        builder.Property(r => r.CompletedOnUtc)
            .HasColumnName("completed_on_utc")
            .IsRequired();

        builder.Property(r => r.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .IsRequired();
    }
}
