using PPECB.Application.Common;
using PPECB.Application.DTOs;

namespace PPECB.Application.Abstractions;

/// <summary>Identity of the caller, resolved from the JWT by the API layer.</summary>
public interface ICurrentUserService
{
    /// <summary>The authenticated user's id, or null when the request is anonymous.</summary>
    string? UserId { get; }

    string? Email { get; }

    bool IsAuthenticated { get; }

    /// <summary>The user id, throwing when unauthenticated. Use inside authorised paths.</summary>
    string RequireUserId();
}

/// <summary>Abstracts the system clock so time-dependent logic stays testable.</summary>
public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}

/// <summary>Generates product codes in the format yyyyMM-### (e.g. 202105-023).</summary>
public interface IProductCodeGenerator
{
    Task<string> GenerateAsync(CancellationToken ct = default);
}

/// <summary>Persists uploaded product images and returns a web-relative path.</summary>
public interface IFileStorageService
{
    Task<string> SaveProductImageAsync(Stream content, string originalFileName, CancellationToken ct = default);

    void DeleteProductImage(string? webRelativePath);
}

/// <summary>Reads and writes the product Excel workbook.</summary>
public interface IExcelService
{
    byte[] ExportProducts(IEnumerable<ProductDto> products);

    /// <summary>Parses an upload into rows. Structural problems are reported, not thrown.</summary>
    ExcelImportParseResult ParseProducts(Stream workbook);

    /// <summary>A blank workbook with the expected headers, to guide users preparing an upload.</summary>
    byte[] BuildImportTemplate();
}

public interface ITokenService
{
    /// <summary>Issues a signed JWT for the given user.</summary>
    (string Token, DateTime ExpiresAtUtc) CreateToken(string userId, string email);
}

public interface ICategoryService
{
    Task<PagedResult<CategoryDto>> GetPagedAsync(int pageNumber, int pageSize, string? search, CancellationToken ct = default);
    Task<IReadOnlyList<CategoryLookupDto>> GetActiveLookupAsync(CancellationToken ct = default);
    Task<CategoryDto> GetByIdAsync(int categoryId, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, CancellationToken ct = default);
    Task<CategoryDto> UpdateAsync(int categoryId, UpdateCategoryDto dto, CancellationToken ct = default);
}

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedAsync(
        int pageNumber, int pageSize, string? search, int? categoryId, CancellationToken ct = default);

    Task<ProductDto> GetByIdAsync(int productId, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto dto, CancellationToken ct = default);
    Task<ProductDto> UpdateAsync(int productId, UpdateProductDto dto, CancellationToken ct = default);
    Task DeleteAsync(int productId, CancellationToken ct = default);

    Task<ProductDto> SetImageAsync(int productId, Stream image, string fileName, CancellationToken ct = default);

    Task<byte[]> ExportToExcelAsync(CancellationToken ct = default);
    Task<ExcelImportResultDto> ImportFromExcelAsync(Stream workbook, CancellationToken ct = default);
}

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
}
