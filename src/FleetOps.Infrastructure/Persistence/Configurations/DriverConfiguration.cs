namespace FleetOps.Infrastructure.Persistence.Configurations;

using FleetOps.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class DriverConfiguration : IEntityTypeConfiguration<Driver>
{
    public void Configure(EntityTypeBuilder<Driver> builder)
    {
        builder.ToTable("drivers");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(d => d.FullName)
            .HasMaxLength(150)
            .HasColumnName("full_name")
            .IsRequired();

        builder.Property(d => d.LicenseNumber)
            .HasMaxLength(30)
            .HasColumnName("license_number")
            .IsRequired();

        builder.Property(d => d.Email)
            .HasMaxLength(150)
            .HasColumnName("email")
            .IsRequired();

        builder.Property(d => d.PhoneNumber)
            .HasMaxLength(30)
            .HasColumnName("phone_number")
            .IsRequired();

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasColumnName("status")
            .IsRequired();

        builder.Property(d => d.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired(false);

        builder.HasIndex(d => d.LicenseNumber)
            .IsUnique()
            .HasDatabaseName("uq_drivers_license_number");

        builder.Property<uint>("Version").IsRowVersion();

        builder.Ignore(d => d.DomainEvents);
    }
}
