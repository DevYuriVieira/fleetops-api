namespace FleetOps.Infrastructure.Persistence.Repositories;

using FleetOps.Application.Abstractions.Persistence;
using FleetOps.Infrastructure.Persistence;
using FleetOps.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

public sealed class MaintenanceCompletionRecordRepository : IMaintenanceCompletionRecordRepository
{
    private readonly FleetOpsDbContext _context;

    public MaintenanceCompletionRecordRepository(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<bool> ExistsAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        return await _context.MaintenanceCompletionRecords
            .AnyAsync(r => r.MessageId == messageId, cancellationToken);
    }

    public async Task AddAsync(
        Guid messageId,
        Guid maintenanceId,
        Guid vehicleId,
        DateTimeOffset completedOnUtc,
        DateTimeOffset processedOnUtc,
        CancellationToken cancellationToken = default)
    {
        var record = new MaintenanceCompletionRecord(
            messageId,
            maintenanceId,
            vehicleId,
            completedOnUtc,
            processedOnUtc);

        await _context.MaintenanceCompletionRecords.AddAsync(record, cancellationToken);
    }
}
