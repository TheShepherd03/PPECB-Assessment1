using Microsoft.EntityFrameworkCore;
using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Domain.Entities;

namespace PPECB.Infrastructure.Persistence.Repositories;

public class CategoryRepository : RepositoryBase<Category>, ICategoryRepository
{
    public CategoryRepository(ApplicationDbContext context) : base(context) { }

    public override Task<Category?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Set.FirstOrDefaultAsync(c => c.CategoryId == id, ct);

    public async Task<PagedResult<Category>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, CancellationToken ct = default)
    {
        var query = Set.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.CategoryCode.Contains(term));
        }

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(c => c.Name)
            .ThenBy(c => c.CategoryId)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<Category>(items, totalCount, pageNumber, pageSize);
    }

    public async Task<IReadOnlyList<Category>> GetActiveAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);

    public Task<bool> CodeExistsAsync(string categoryCode, int? excludeCategoryId, CancellationToken ct = default) =>
        Set.AsNoTracking()
            .Where(c => c.CategoryCode == categoryCode)
            .Where(c => excludeCategoryId == null || c.CategoryId != excludeCategoryId.Value)
            .AnyAsync(ct);

    public Task<Category?> GetByCodeAsync(string categoryCode, CancellationToken ct = default) =>
        Set.AsNoTracking().FirstOrDefaultAsync(c => c.CategoryCode == categoryCode, ct);

    public Task<bool> HasProductsAsync(int categoryId, CancellationToken ct = default) =>
        Context.Products.AsNoTracking().AnyAsync(p => p.CategoryId == categoryId, ct);
}
