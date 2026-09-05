namespace FleetOps.UnitTests.Domain;

using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Events;
using FleetOps.Domain.Exceptions;

public class DriverTests
{
    private static Driver CreateValidDriver()
    {
        return Driver.Create(
            Guid.NewGuid(),
            "John Doe",
            "DL12345678",
            "john.doe@fleetops.com",
            "+1234567890");
    }

    [Fact]
    public void Create_ShouldInstantiateDriver_WhenDataIsValid()
    {
        var id = Guid.NewGuid();
        var driver = Driver.Create(
            id,
            " Jane Smith ",
            "dl98765432",
            "JANE.SMITH@FLEETOPS.COM",
            "+9876543210");

        Assert.Equal(id, driver.Id);
        Assert.Equal("Jane Smith", driver.FullName);
        Assert.Equal("DL98765432", driver.LicenseNumber);
        Assert.Equal("jane.smith@fleetops.com", driver.Email);
        Assert.Equal("+9876543210", driver.PhoneNumber);
        Assert.Equal(DriverStatus.Active, driver.Status);
    }

    [Fact]
    public void Driver_ShouldNotExposeCurrentVehicleId()
    {
        var property = typeof(Driver).GetProperty("CurrentVehicleId");
        Assert.Null(property);
    }

    [Theory]
    [InlineData("", "DL123", "test@test.com", "12345")]
    [InlineData("Name", "", "test@test.com", "12345")]
    [InlineData("Name", "DL123", "invalid-email", "12345")]
    [InlineData("Name", "DL123", "test@test.com", "")]
    public void Create_ShouldThrowValidationException_WhenFieldIsInvalid(
        string name,
        string license,
        string email,
        string phone)
    {
        Assert.Throws<DomainValidationException>(() => Driver.Create(
            Guid.NewGuid(),
            name,
            license,
            email,
            phone));
    }

    [Fact]
    public void Suspend_ShouldChangeStatusToSuspendedAndRaiseDomainEvent()
    {
        var driver = CreateValidDriver();
        driver.ClearDomainEvents();

        driver.Suspend("License renewal pending");

        Assert.Equal(DriverStatus.Suspended, driver.Status);
        Assert.Contains(driver.DomainEvents, e => e is DriverSuspendedDomainEvent);
    }

    [Fact]
    public void Suspend_ShouldThrowInvalidStateException_WhenAlreadySuspended()
    {
        var driver = CreateValidDriver();
        driver.Suspend("Infraction");

        Assert.Throws<InvalidDriverStateException>(() => driver.Suspend("Another infraction"));
    }

    [Fact]
    public void Deactivate_ShouldChangeStatusToInactiveAndRaiseDomainEvent()
    {
        var driver = CreateValidDriver();
        driver.ClearDomainEvents();

        driver.Deactivate();

        Assert.Equal(DriverStatus.Inactive, driver.Status);
        Assert.Contains(driver.DomainEvents, e => e is DriverDeactivatedDomainEvent);
    }

    [Fact]
    public void Activate_ShouldChangeStatusToActive_WhenSuspendedOrInactive()
    {
        var driver = CreateValidDriver();
        driver.Suspend("Temporary leave");
        driver.ClearDomainEvents();

        driver.Activate();

        Assert.Equal(DriverStatus.Active, driver.Status);
        Assert.Contains(driver.DomainEvents, e => e is DriverActivatedDomainEvent);
    }

    [Fact]
    public void Activate_ShouldThrowInvalidStateException_WhenAlreadyActive()
    {
        var driver = CreateValidDriver();

        Assert.Throws<InvalidDriverStateException>(() => driver.Activate());
    }
}
