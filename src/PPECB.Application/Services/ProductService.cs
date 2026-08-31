using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Application.DTOs;
using PPECB.Application.Mapping;
using PPECB.Application.Validation;
using PPECB.Domain.Entities;
using PPECB.Domain.Exceptions;

namespace PPECB.Application.Services;

public class ProductService : IProductService
{
    /// <summary>
    /// How many times to re-generate a product code when a concurrent create takes the
    /// one we picked. Collisions need two creates in the same millisecond window, so a
    /// small number of attempts is ample.
    /// </summary>
    private const int MaxCodeGenerationAttempts = 5;

    private readonly IProductRepository _products;
    private readonly ICategoryRepository _categories;
    private readonly IProductCodeGenerator _codeGenerator;
    private readonly IFileStorageService _files;
    private readonly IExcelService _excel;
    private readonly IUnitOfWork _unitOfWork;

    public ProductService(
        IProductRepository products,
        ICategoryRepository categories,
        IProductCodeGenerator codeGenerator,
        IFileStorageService files,
        IExcelService excel,
        IUnitOfWork unitOfWork)
    {
        _products = products;
        _categories = categories;
        _codeGenerator = codeGenerator;
        _files = files;
        _excel = excel;
        _unitOfWork = unitOfWork;
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, int? categoryId, CancellationToken ct = default)
    {
        var page = await _products.GetPagedAsync(
            PagingDefaults.NormalisePageNumber(pageNumber),
            PagingDefaults.NormalisePageSize(pageSize),
            search,
            categoryId,
            ct);

        return page.Map(p => p.ToDto());
    }

    public async Task<ProductDto> GetByIdAsync(int productId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdWithCategoryAsync(productId, ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        return product.ToDto();
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default)
    {
        var category = await RequireCategoryAsync(dto.CategoryId, ct);

        var product = new Product
        {
            Name = dto.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim(),
            CategoryId = category.CategoryId,
            Price = dto.Price
        };

        await _products.AddAsync(product, ct);
        await SaveWithGeneratedCodeAsync(product, ct);

        product.Category = category;
        return product.ToDto();
    }

    public async Task<ProductDto> UpdateAsync(int productId, UpdateProductDto dto, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        var category = await RequireCategoryAsync(dto.CategoryId, ct);

        product.Name = dto.Name.Trim();
        product.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        product.CategoryId = category.CategoryId;
        product.Price = dto.Price;

        var original = EntityMappings.FromBase64(dto.RowVersion);
        if (original is not null)
        {
            _products.SetOriginalRowVersion(product, original);
        }

        _products.Update(product);
        await _unitOfWork.SaveChangesAsync(ct);

        product.Category = category;
        return product.ToDto();
    }

    public async Task DeleteAsync(int productId, CancellationToken ct = default)
    {
        var product = await _products.GetByIdAsync(productId, ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        var imageToRemove = product.ImagePath;

        _products.Remove(product);
        await _unitOfWork.SaveChangesAsync(ct);

        // Only unlink the file once the row is definitely gone, so a failed delete
        // cannot leave a product pointing at a missing image.
        _files.DeleteProductImage(imageToRemove);
    }

    public async Task<ProductDto> SetImageAsync(
        int productId, Stream image, string fileName, CancellationToken ct = default)
    {
        var product = await _products.GetByIdWithCategoryAsync(productId, ct)
                      ?? throw new NotFoundException(nameof(Product), productId);

        var previousImage = product.ImagePath;
        product.ImagePath = await _files.SaveProductImageAsync(image, fileName, ct);

        _products.Update(product);
        await _unitOfWork.SaveChangesAsync(ct);

        if (!string.Equals(previousImage, product.ImagePath, StringComparison.Ordinal))
        {
            _files.DeleteProductImage(previousImage);
        }

        return product.ToDto();
    }

    public async Task<byte[]> ExportToExcelAsync(CancellationToken ct = default)
    {
        var products = await _products.GetAllWithCategoryAsync(ct);
        return _excel.ExportProducts(products.Select(p => p.ToDto()));
    }

    /// <summary>
    /// Imports products from a workbook. Validation runs over every row first; if any row
    /// is bad nothing is written, so the user never ends up with a half-loaded file.
    /// </summary>
    public async Task<ExcelImportResultDto> ImportFromExcelAsync(Stream workbook, CancellationToken ct = default)
    {
        var parsed = _excel.ParseProducts(workbook);

        var result = new ExcelImportResultDto
        {
            RowsRead = parsed.Rows.Count,
            Errors = new List<ExcelImportErrorDto>(parsed.Errors)
        };

        if (parsed.Rows.Count == 0 && result.Errors.Count == 0)
        {
            result.Errors.Add(new ExcelImportErrorDto(0, "The workbook contains no product rows."));
        }

        // Resolve categories once by code, then by name, so the spreadsheet can use either.
        var categories = await _categories.GetActiveAsync(ct);
        var byCode = categories.ToDictionary(c => c.CategoryCode, StringComparer.OrdinalIgnoreCase);
        var byName = categories
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var staged = new List<Product>();

        foreach (var row in parsed.Rows)
        {
            if (string.IsNullOrWhiteSpace(row.Name))
            {
                result.Errors.Add(new ExcelImportErrorDto(row.RowNumber, "Name is required."));
                continue;
            }

            var category = ResolveCategory(row, byCode, byName, out var categoryError);
            if (category is null)
            {
                result.Errors.Add(new ExcelImportErrorDto(row.RowNumber, categoryError!));
                continue;
            }

            if (row.Price is null)
            {
                result.Errors.Add(new ExcelImportErrorDto(
                    row.RowNumber,
                    string.IsNullOrWhiteSpace(row.PriceRaw)
                        ? "Price is required."
                        : $"Price '{row.PriceRaw}' is not a valid number."));
                continue;
            }

            if (row.Price < 0)
            {
                result.Errors.Add(new ExcelImportErrorDto(row.RowNumber, "Price cannot be negative."));
                continue;
            }

            staged.Add(new Product
            {
                Name = row.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(row.Description) ? null : row.Description.Trim(),
                CategoryId = category.CategoryId,
                Price = row.Price.Value
            });
        }

        if (result.Errors.Count > 0)
        {
            result.Succeeded = false;
            result.ProductsImported = 0;
            return result;
        }

        // Codes are assigned sequentially in memory, then written in a single transaction.
        var nextCode = await _codeGenerator.GenerateAsync(ct);
        var prefix = nextCode.Split('-')[0];
        var sequence = CodeFormats.TryGetSequence(nextCode) ?? 1;

        foreach (var product in staged)
        {
            product.ProductCode = CodeFormats.FormatProductCode(prefix, sequence++);
        }

        await _products.AddRangeAsync(staged, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        result.Succeeded = true;
        result.ProductsImported = staged.Count;
        return result;
    }

    private static Category? ResolveCategory(
        ExcelProductRow row,
        IReadOnlyDictionary<string, Category> byCode,
        IReadOnlyDictionary<string, Category> byName,
        out string? error)
    {
        error = null;

        if (!string.IsNullOrWhiteSpace(row.CategoryCode))
        {
            var code = CodeFormats.NormaliseCategoryCode(row.CategoryCode);
            if (byCode.TryGetValue(code, out var match)) return match;

            error = $"No active category found with code '{code}'.";
            return null;
        }

        if (!string.IsNullOrWhiteSpace(row.CategoryName))
        {
            if (byName.TryGetValue(row.CategoryName.Trim(), out var match)) return match;

            error = $"No active category found named '{row.CategoryName.Trim()}'.";
            return null;
        }

        error = "A category code or category name is required.";
        return null;
    }

    private async Task<Category> RequireCategoryAsync(int categoryId, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(categoryId, ct);

        if (category is null)
        {
            // The global ownership filter means another user's category reads as missing,
            // which is what we want: existence is not disclosed across accounts.
            throw new Domain.Exceptions.ValidationException(
                nameof(CreateProductDto.CategoryId), "Please select a valid category.");
        }

        if (!category.IsActive)
        {
            throw new Domain.Exceptions.ValidationException(
                nameof(CreateProductDto.CategoryId), $"Category '{category.Name}' is inactive.");
        }

        return category;
    }

    /// <summary>
    /// Saves a new product, re-generating its code if a concurrent insert claimed it first.
    /// </summary>
    private async Task SaveWithGeneratedCodeAsync(Product product, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            product.ProductCode = await _codeGenerator.GenerateAsync(ct);

            try
            {
                await _unitOfWork.SaveChangesAsync(ct);
                return;
            }
            catch (DuplicateKeyException) when (attempt < MaxCodeGenerationAttempts)
            {
                // Someone else took this code. Loop and ask for the next one.
            }
        }
    }
}
