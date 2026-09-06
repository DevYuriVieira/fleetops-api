namespace FleetOps.Application.UseCases.Maintenance;

using FleetOps.Application.Abstractions.Persistence;

public sealed record ProcessMaintenanceCompletedCommand(
    Guid MessageId,
    Guid MaintenanceId,
    Guid VehicleId,
    DateTimeOffset CompletedOnUtc);

public sealed class ProcessMaintenanceCompletedUseCase
{
    private readonly IMaintenanceCompletionRecordRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public ProcessMaintenanceCompletedUseCase(
        IMaintenanceCompletionRecordRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> ExecuteAsync(ProcessMaintenanceCompletedCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (await _repository.ExistsAsync(command.MessageId, cancellationToken))
        {
            return false;
        }

        await _repository.AddAsync(
            command.MessageId,
            command.MaintenanceId,
            command.VehicleId,
            command.CompletedOnUtc,
            DateTimeOffset.UtcNow,
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }
}
