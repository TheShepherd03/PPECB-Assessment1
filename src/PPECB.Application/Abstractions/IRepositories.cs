using PPECB.Application.Common;
using PPECB.Domain.Entities;

namespace PPECB.Application.Abstractions;

/// <summary>
/// Repository pattern. The Application layer talks to these interfaces only, so it has
/// no compile-time dependency on Entity Framework and can be unit tested with fakes.
/// Implementations apply the current user's ownership filter automatically.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);

    /// <summary>
    /// Tells the change tracker which rowversion the caller's edit was based on. The
    /// optimistic-concurrency check compares the *original* value, so assigning the
    /// entity's property is not enough — it has to be set on the tracked entry.
    /// </summary>
    void SetOriginalRowVersion(T entity, byte[] rowVersion);
}

public interface ICategoryRepository : IRepository<Category>
{
    Task<PagedResult<Category>> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default);

    Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>True when the code is already taken by another category of the same owner.</summary>
    Task<bool> CodeExistsAsync(string categoryCode, int? excludeCategoryId, CancellationToken ct = default);

    Task<Category?> GetByCodeAsync(string categoryCode, CancellationToken ct = default);

    Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default);
}

public interface IProductRepository : IRepository<Product>
{
    Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, int? categoryId, CancellationToken ct = default);

    Task<Product?> GetByIdWithCategoryAsync(int productId, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetAllWithCategoryAsync(CancellationToken ct = default);

    /// <summary>
    /// Highest sequence number already issued for the given yyyyMM prefix, or 0 when the
    /// month has no products yet. Used to generate the next product code.
    /// </summary>
    Task<int> GetMaxSequenceForPrefixAsync(string prefix, CancellationToken ct = default);

    Task AddRangeAsync(IEnumerable<Product> products, CancellationToken ct = default);
}

/// <summary>
/// Unit of Work. Repositories stage changes; the service layer decides the transaction
/// boundary by calling SaveChangesAsync once per use case.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
