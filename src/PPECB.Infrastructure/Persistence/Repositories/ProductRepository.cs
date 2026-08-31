using Microsoft.EntityFrameworkCore;
using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Application.Validation;
using PPECB.Domain.Entities;

namespace PPECB.Infrastructure.Persistence.Repositories;

public class ProductRepository : RepositoryBase<Product>, IProductRepository
{
    public ProductRepository(ApplicationDbContext context) : base(context) { }

    public override Task<Product?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(p => p.ProductId == id, ct);

    public Task<Product?> GetByIdWithCategoryAsync(int productId, CancellationToken ct = default) =>
        Set.Include(p => p.Category).FirstOrDefaultAsync(p => p.ProductId == productId, ct);

    public async Task<PagedResult<Product>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, int? categoryId, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().Include(p => p.Category).AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(p =>
                p.Name.Contains(term) ||
                p.ProductCode.Contains(term) ||
                (p.Description != null && p.Description.Contains(term)));
        }

        if (categoryId is > 0)
        {
            query = query.Where(p => p.CategoryId == categoryId.Value);
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(p => p.CreatedDate)
            .ThenByDescending(p => p.ProductId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Product>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Product>> GetAllWithCategoryAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Include(p => p.Category)
            .OrderBy(p => p.ProductCode)
            .ToListAsync(ct);

    /// <summary>
    /// Only the codes for the given month are pulled back — a short list of small strings —
    /// and the sequence is parsed in memory, which keeps the format rule in one place
    /// rather than duplicating it as SQL string surgery.
    /// </summary>
    public async Task<int> GetMaxSequenceForPrefixAsync(string prefix, CancellationToken ct = default)
    {
        var codes = await Set.AsNoTracking()
            .Where(p => p.ProductCode.StartsWith(prefix + "-"))
            .Select(p => p.ProductCode)
            .ToListAsync(ct);

        return codes
            .Select(CodeFormats.TryGetSequence)
            .Where(sequence => sequence.HasValue)
            .Select(sequence => sequence!.Value)
            .DefaultIfEmpty(0)
            .Max();
    }

    public async Task AddRangeAsync(IEnumerable<Product> products, CancellationToken ct = default) =>
        await Set.AddRangeAsync(products, ct);
}
