using Microsoft.EntityFrameworkCore;
using PPECB.Application.Abstractions;
using PPECB.Domain.Common;

namespace PPECB.Infrastructure.Persistence.Repositories;

/// <summary>
/// Shared EF Core plumbing. Ownership filtering is not repeated here — it is applied by
/// the global query filters on <see cref="ApplicationDbContext"/>.
/// </summary>
public abstract class RepositoryBase<T> : IRepository<T> where T : class
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<T> Set;

    protected RepositoryBase(ApplicationDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    /// <summary>
    /// Deliberately abstract rather than using <c>DbSet.FindAsync</c>: Find can return an
    /// entity already in the change tracker without re-running the global query filter,
    /// which would be a way to read another user's row. Subclasses issue a real query.
    /// </summary>
    public abstract Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);

    public void SetOriginalRowVersion(T entity, byte[] rowVersion) =>
        Context.Entry(entity)
            .Property(nameof(AuditableEntity.RowVersion))
            .OriginalValue = rowVersion;
}
