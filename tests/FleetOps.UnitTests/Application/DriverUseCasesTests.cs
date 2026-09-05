namespace FleetOps.UnitTests.Application;

using FleetOps.Application.Exceptions;
using FleetOps.Application.UseCases.Drivers;
using FleetOps.Domain.Entities;
using FleetOps.Domain.Enums;
using FleetOps.Domain.Exceptions;
using FleetOps.UnitTests.Application.Doubles;

public sealed class DriverUseCasesTests
{
    private readonly InMemoryDriverRepository _driverRepository = new();
    private readonly InMemoryUnitOfWork _unitOfWork = new();

    private Driver CreateTestDriver(DriverStatus status = DriverStatus.Active)
    {
        var driver = Driver.Create(
            Guid.NewGuid(),
            "Carlos Silva",
            "CNH12345678",
            "carlos.silva@fleetops.com",
            "+5511988887777");

        if (status == DriverStatus.Suspended)
        {
            driver.Suspend("Pending medical exam");
        }
        else if (status == DriverStatus.Inactive)
        {
            driver.Deactivate();
        }

        return driver;
    }

    [Fact]
    public async Task RegisterDriver_WithValidCommand_ShouldCreateAndPersistDriver()
    {
        var useCase = new RegisterDriverUseCase(_driverRepository, _unitOfWork);
        var command = new RegisterDriverCommand(
            "Maria Santos",
            "CNH87654321",
            "maria.santos@fleetops.com",
            "+5511977776666");

        var result = await useCase.ExecuteAsync(command);

        Assert.NotNull(result);
        Assert.Equal("Maria Santos", result.FullName);
        Assert.Equal("CNH87654321", result.LicenseNumber);
        Assert.Equal("maria.santos@fleetops.com", result.Email);
        Assert.Equal(DriverStatus.Active.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);

        var stored = await _driverRepository.GetByIdAsync(result.Id);
        Assert.NotNull(stored);
        Assert.Equal("CNH87654321", stored.LicenseNumber);
    }

    [Fact]
    public async Task RegisterDriver_WhenLicenseAlreadyExists_ShouldThrowConflictException()
    {
        var existing = CreateTestDriver();
        await _driverRepository.AddAsync(existing);

        var useCase = new RegisterDriverUseCase(_driverRepository, _unitOfWork);
        var command = new RegisterDriverCommand(
            "Another Driver",
            existing.LicenseNumber,
            "another@fleetops.com",
            "+5511966665555");

        await Assert.ThrowsAsync<ConflictException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task RegisterDriver_WithInvalidEmail_ShouldThrowDomainValidationException()
    {
        var useCase = new RegisterDriverUseCase(_driverRepository, _unitOfWork);
        var command = new RegisterDriverCommand(
            "Test Driver",
            "CNH99999999",
            "invalid-email",
            "+5511966665555");

        await Assert.ThrowsAsync<DomainValidationException>(() => useCase.ExecuteAsync(command));
    }

    [Fact]
    public async Task ActivateDriver_WhenInactive_ShouldActivateDriver()
    {
        var driver = CreateTestDriver(DriverStatus.Inactive);
        await _driverRepository.AddAsync(driver);

        var useCase = new ActivateDriverUseCase(_driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new ActivateDriverCommand(driver.Id));

        Assert.Equal(DriverStatus.Active.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task ActivateDriver_WhenNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new ActivateDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new ActivateDriverCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task ActivateDriver_WhenAlreadyActive_ShouldThrowInvalidDriverStateException()
    {
        var driver = CreateTestDriver(DriverStatus.Active);
        await _driverRepository.AddAsync(driver);

        var useCase = new ActivateDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<InvalidDriverStateException>(() => useCase.ExecuteAsync(new ActivateDriverCommand(driver.Id)));
    }

    [Fact]
    public async Task SuspendDriver_WhenActive_ShouldSuspendDriver()
    {
        var driver = CreateTestDriver(DriverStatus.Active);
        await _driverRepository.AddAsync(driver);

        var useCase = new SuspendDriverUseCase(_driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new SuspendDriverCommand(driver.Id, "Traffic violation pending"));

        Assert.Equal(DriverStatus.Suspended.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task SuspendDriver_WhenNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new SuspendDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new SuspendDriverCommand(Guid.NewGuid(), "Reason")));
    }

    [Fact]
    public async Task SuspendDriver_WhenAlreadySuspended_ShouldThrowInvalidDriverStateException()
    {
        var driver = CreateTestDriver(DriverStatus.Suspended);
        await _driverRepository.AddAsync(driver);

        var useCase = new SuspendDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<InvalidDriverStateException>(() => useCase.ExecuteAsync(new SuspendDriverCommand(driver.Id, "Reason")));
    }

    [Fact]
    public async Task DeactivateDriver_WhenActive_ShouldDeactivateDriver()
    {
        var driver = CreateTestDriver(DriverStatus.Active);
        await _driverRepository.AddAsync(driver);

        var useCase = new DeactivateDriverUseCase(_driverRepository, _unitOfWork);
        var result = await useCase.ExecuteAsync(new DeactivateDriverCommand(driver.Id));

        Assert.Equal(DriverStatus.Inactive.ToString(), result.Status);
        Assert.Equal(1, _unitOfWork.SaveChangesCallCount);
    }

    [Fact]
    public async Task DeactivateDriver_WhenNotFound_ShouldThrowNotFoundException()
    {
        var useCase = new DeactivateDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<NotFoundException>(() => useCase.ExecuteAsync(new DeactivateDriverCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task DeactivateDriver_WhenAlreadyInactive_ShouldThrowInvalidDriverStateException()
    {
        var driver = CreateTestDriver(DriverStatus.Inactive);
        await _driverRepository.AddAsync(driver);

        var useCase = new DeactivateDriverUseCase(_driverRepository, _unitOfWork);
        await Assert.ThrowsAsync<InvalidDriverStateException>(() => useCase.ExecuteAsync(new DeactivateDriverCommand(driver.Id)));
    }
}
