using PPECB.Application.Abstractions;

namespace PPECB.Infrastructure.Persistence;

/// <summary>
/// Thin wrapper over the DbContext's save. Exception translation and audit stamping live
/// in <see cref="ApplicationDbContext.SaveChangesAsync"/> so every write path gets them.
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly ApplicationDbContext _context;

    public UnitOfWork(ApplicationDbContext context) => _context = context;

    public Task<int> SaveChangesAsync(CancellationToken ct = default) =>
        _context.SaveChangesAsync(ct);
}
