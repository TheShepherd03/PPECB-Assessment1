using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Application.DTOs;
using PPECB.Application.Mapping;
using PPECB.Application.Validation;
using PPECB.Domain.Entities;
using PPECB.Domain.Exceptions;

namespace PPECB.Application.Services;

public class CategoryService : ICategoryService
{
    private readonly ICategoryRepository _categories;
    private readonly IUnitOfWork _unitOfWork;

    public CategoryService(ICategoryRepository categories, IUnitOfWork unitOfWork)
    {
        _categories = categories;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<CategoryDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, CancellationToken ct = default)
    {
        var page = await _categories.GetPagedAsync(
            PagingDefaults.NormalisePageNumber(pageNumber),
            PagingDefaults.NormalisePageSize(pageSize),
            search,
            ct);

        return page.Map(c => c.ToDto());
    }

    public async Task<IReadOnlyList<CategoryLookupDto>> GetActiveLookupAsync(CancellationToken ct = default)
    {
        var categories = await _categories.GetActiveAsync(ct);
        return categories.Select(c => c.ToLookupDto()).ToList();
    }

    public async Task<CategoryDto> GetByIdAsync(int categoryId, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(categoryId, ct)
                       ?? throw new NotFoundException(nameof(Category), categoryId);

        return category.ToDto();
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default)
    {
        var code = CodeFormats.NormaliseCategoryCode(dto.CategoryCode);
        await GuardCategoryCodeAsync(code, excludeCategoryId: null, ct);

        var category = new Category
        {
            Name = dto.Name.Trim(),
            CategoryCode = code,
            IsActive = dto.IsActive
        };

        await _categories.AddAsync(category, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return category.ToDto();
    }

    public async Task<CategoryDto> UpdateAsync(int categoryId, UpdateCategoryDto dto, CancellationToken ct = default)
    {
        var category = await _categories.GetByIdAsync(categoryId, ct)
                       ?? throw new NotFoundException(nameof(Category), categoryId);

        var code = CodeFormats.NormaliseCategoryCode(dto.CategoryCode);
        await GuardCategoryCodeAsync(code, excludeCategoryId: categoryId, ct);

        category.Name = dto.Name.Trim();
        category.CategoryCode = code;
        category.IsActive = dto.IsActive;

        // Compare against the rowversion the user actually saw, not the one just loaded.
        var original = EntityMappings.FromBase64(dto.RowVersion);
        if (original is not null)
        {
            _categories.SetOriginalRowVersion(category, original);
        }

        _categories.Update(category);
        await _unitOfWork.SaveChangesAsync(ct);

        return category.ToDto();
    }

    /// <summary>
    /// Enforces both category code rules from the brief: the required format, and
    /// uniqueness within the owner's categories.
    /// </summary>
    private async Task GuardCategoryCodeAsync(string code, int? excludeCategoryId, CancellationToken ct)
    {
        if (!CodeFormats.IsValidCategoryCode(code))
        {
            throw new Domain.Exceptions.ValidationException(
                nameof(CreateCategoryDto.CategoryCode), CodeFormats.CategoryCodeDescription);
        }

        if (await _categories.CodeExistsAsync(code, excludeCategoryId, ct))
        {
            throw new Domain.Exceptions.ValidationException(
                nameof(CreateCategoryDto.CategoryCode), $"Category code '{code}' is already in use.");
        }
    }
}
