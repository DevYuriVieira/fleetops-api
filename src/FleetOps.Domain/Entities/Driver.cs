namespace FleetOps.Domain.Entities;

using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;
using FleetOps.Domain.Primitives;

public sealed class Driver : AggregateRoot
{
    public string FullName { get; private set; }
    public string LicenseNumber { get; private set; }
    public string Email { get; private set; }
    public string PhoneNumber { get; private set; }
    public DriverStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private Driver()
    {
        FullName = null!;
        LicenseNumber = null!;
        Email = null!;
        PhoneNumber = null!;
    }

    private Driver(
        Guid id,
        string fullName,
        string licenseNumber,
        string email,
        string phoneNumber,
        DateTimeOffset createdAt) : base(id)
    {
        FullName = fullName;
        LicenseNumber = licenseNumber;
        Email = email;
        PhoneNumber = phoneNumber;
        Status = DriverStatus.Active;
        CreatedAt = createdAt;
        UpdatedAt = null;
    }

    public static Driver Create(
        Guid id,
        string fullName,
        string licenseNumber,
        string email,
        string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            throw new DomainValidationException("Driver full name cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(licenseNumber))
        {
            throw new DomainValidationException("Driver license number cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
        {
            throw new DomainValidationException("Driver email is invalid.");
        }

        if (string.IsNullOrWhiteSpace(phoneNumber))
        {
            throw new DomainValidationException("Driver phone number cannot be empty.");
        }

        return new Driver(
            id,
            fullName.Trim(),
            licenseNumber.Trim().ToUpperInvariant(),
            email.Trim().ToLowerInvariant(),
            phoneNumber.Trim(),
            DateTimeOffset.UtcNow);
    }

    public void Activate()
    {
        if (Status == DriverStatus.Active)
        {
            throw new InvalidDriverStateException("Driver is already active.");
        }

        Status = DriverStatus.Active;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DriverActivatedDomainEvent(Id, UpdatedAt.Value));
    }

    public void Suspend(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainValidationException("Suspension reason cannot be empty.");
        }

        if (Status == DriverStatus.Suspended)
        {
            throw new InvalidDriverStateException("Driver is already suspended.");
        }

        Status = DriverStatus.Suspended;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DriverSuspendedDomainEvent(Id, reason.Trim(), UpdatedAt.Value));
    }

    public void Deactivate()
    {
        if (Status == DriverStatus.Inactive)
        {
            throw new InvalidDriverStateException("Driver is already inactive.");
        }

        Status = DriverStatus.Inactive;
        UpdatedAt = DateTimeOffset.UtcNow;

        RaiseDomainEvent(new DriverDeactivatedDomainEvent(Id, UpdatedAt.Value));
    }
}
