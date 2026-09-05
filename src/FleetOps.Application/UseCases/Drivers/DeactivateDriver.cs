namespace FleetOps.Application.UseCases.Drivers;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record DeactivateDriverCommand(Guid DriverId);

public sealed class DeactivateDriverUseCase
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeactivateDriverUseCase(IDriverRepository driverRepository, IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverDto> ExecuteAsync(DeactivateDriverCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var driver = await _driverRepository.GetByIdAsync(command.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", command.DriverId);

        driver.Deactivate();

        await _driverRepository.UpdateAsync(driver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return driver.ToDto();
    }
}
