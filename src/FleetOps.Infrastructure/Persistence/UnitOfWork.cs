namespace FleetOps.Infrastructure.Persistence;

using FleetOps.Application.Abstractions.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly FleetOpsDbContext _context;

    public UnitOfWork(FleetOpsDbContext context)
    {
        _context = context;
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            var mapped = DatabaseExceptionMapper.Map(ex);
            if (!ReferenceEquals(mapped, ex))
            {
                throw mapped;
            }

            throw;
        }
    }
}
