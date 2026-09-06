namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("processed_messages");

        builder.HasKey(m => new { m.MessageId, m.Consumer });

        builder.Property(m => m.MessageId)
            .HasColumnName("message_id")
            .ValueGeneratedNever();

        builder.Property(m => m.Consumer)
            .HasMaxLength(128)
            .HasColumnName("consumer")
            .IsRequired();

        builder.Property(m => m.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .IsRequired();

        builder.HasIndex(m => m.ProcessedOnUtc)
            .HasDatabaseName("ix_processed_messages_processed_on");
    }
}
