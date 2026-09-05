namespace FleetOps.Application.UseCases.Drivers;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;
using FleetOps.Domain.Entities;

public sealed record RegisterDriverCommand(
    string FullName,
    string LicenseNumber,
    string Email,
    string PhoneNumber);

public sealed class RegisterDriverUseCase
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterDriverUseCase(IDriverRepository driverRepository, IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverDto> ExecuteAsync(RegisterDriverCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedLicense = command.LicenseNumber?.Trim().ToUpperInvariant() ?? string.Empty;
        var exists = await _driverRepository.ExistsByLicenseNumberAsync(normalizedLicense, cancellationToken);
        if (exists)
        {
            throw new ConflictException($"Driver with license number '{normalizedLicense}' is already registered.");
        }

        var driver = Driver.Create(
            Guid.NewGuid(),
            command.FullName,
            command.LicenseNumber!,
            command.Email,
            command.PhoneNumber);

        await _driverRepository.AddAsync(driver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return driver.ToDto();
    }
}
