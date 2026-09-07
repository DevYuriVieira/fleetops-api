namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(m => m.OccurredOnUtc)
            .HasColumnName("occurred_on_utc")
            .IsRequired();

        builder.Property(m => m.EventType)
            .HasMaxLength(200)
            .HasColumnName("event_type")
            .IsRequired();

        builder.Property(m => m.Payload)
            .HasColumnType("text")
            .HasColumnName("payload")
            .IsRequired();

        builder.Property(m => m.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .IsRequired(false);

        builder.Property(m => m.Attempts)
            .HasColumnName("attempts")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(m => m.Error)
            .HasColumnType("text")
            .HasColumnName("error")
            .IsRequired(false);

        builder.Property(m => m.TraceParent)
            .HasMaxLength(128)
            .HasColumnName("trace_parent")
            .IsRequired(false);

        builder.HasIndex(m => m.OccurredOnUtc)
            .HasDatabaseName("ix_outbox_messages_unprocessed")
            .HasFilter("processed_on_utc IS NULL");
    }
}
