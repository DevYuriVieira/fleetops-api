namespace FleetOps.Application.UseCases.Drivers;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Application.DTOs;
using FleetOps.Application.Exceptions;

public sealed record ActivateDriverCommand(Guid DriverId);

public sealed class ActivateDriverUseCase
{
    private readonly IDriverRepository _driverRepository;
    private readonly IUnitOfWork _unitOfWork;

    public ActivateDriverUseCase(IDriverRepository driverRepository, IUnitOfWork unitOfWork)
    {
        _driverRepository = driverRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<DriverDto> ExecuteAsync(ActivateDriverCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var driver = await _driverRepository.GetByIdAsync(command.DriverId, cancellationToken)
            ?? throw new NotFoundException("Driver", command.DriverId);

        driver.Activate();

        await _driverRepository.UpdateAsync(driver, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return driver.ToDto();
    }
}
