using PPECB.Application.DTOs;
using PPECB.Domain.Entities;

namespace PPECB.Application.Mapping;

/// <summary>
/// Hand-written projections. A mapping library would add a dependency and a startup cost
/// for two entities; explicit mapping keeps the shape obvious and is trivial to test.
/// </summary>
public static class EntityMappings
{
    public static CategoryDto ToDto(this Category category) => new()
    {
        CategoryId = category.CategoryId,
        Name = category.Name,
        CategoryCode = category.CategoryCode,
        IsActive = category.IsActive,
        CreatedBy = category.CreatedBy,
        CreatedDate = category.CreatedDate,
        UpdatedBy = category.UpdatedBy,
        UpdatedDate = category.UpdatedDate,
        RowVersion = ToBase64(category.RowVersion)
    };

    public static CategoryLookupDto ToLookupDto(this Category category) => new()
    {
        CategoryId = category.CategoryId,
        Name = category.Name,
        CategoryCode = category.CategoryCode
    };

    public static ProductDto ToDto(this Product product) => new()
    {
        ProductId = product.ProductId,
        ProductCode = product.ProductCode,
        Name = product.Name,
        Description = product.Description,
        CategoryId = product.CategoryId,
        CategoryName = product.Category?.Name ?? string.Empty,
        CategoryCode = product.Category?.CategoryCode ?? string.Empty,
        Price = product.Price,
        ImagePath = product.ImagePath,
        CreatedBy = product.CreatedBy,
        CreatedDate = product.CreatedDate,
        UpdatedBy = product.UpdatedBy,
        UpdatedDate = product.UpdatedDate,
        RowVersion = ToBase64(product.RowVersion)
    };

    private static string? ToBase64(byte[]? rowVersion) =>
        rowVersion is null || rowVersion.Length == 0 ? null : Convert.ToBase64String(rowVersion);

    /// <summary>
    /// Parses a client-supplied rowversion. Returns null when absent so the caller can
    /// decide whether a missing token is acceptable.
    /// </summary>
    public static byte[]? FromBase64(string? rowVersion)
    {
        if (string.IsNullOrWhiteSpace(rowVersion)) return null;
        try
        {
            return Convert.FromBase64String(rowVersion);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
