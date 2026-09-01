using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Application.DTOs;

namespace PPECB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductsController : ControllerBase
{
    private const string ExcelContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>Upper bound on an uploaded workbook, enforced before the file is read.</summary>
    private const long MaxUploadBytes = 10 * 1024 * 1024;

    private readonly IProductService _products;
    private readonly IExcelService _excel;

    public ProductsController(IProductService products, IExcelService excel)
    {
        _products = products;
        _excel = excel;
    }

    /// <summary>Returns a page of the caller's products, 10 per page by default.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = PagingDefaults.DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] int? categoryId = null,
        CancellationToken ct = default)
    {
        var page = await _products.GetPagedAsync(pageNumber, pageSize, search, categoryId, ct);
        return Ok(page);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(int id, CancellationToken ct)
    {
        var product = await _products.GetByIdAsync(id, ct);
        return Ok(product);
    }

    /// <summary>Creates a product. The product code is generated server-side.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductDto request, CancellationToken ct)
    {
        var created = await _products.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.ProductId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Update(
        int id, [FromBody] UpdateProductDto request, CancellationToken ct)
    {
        var updated = await _products.UpdateAsync(id, request, ct);
        return Ok(updated);
    }

    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _products.DeleteAsync(id, ct);
        return NoContent();
    }

    /// <summary>Uploads or replaces the product's image.</summary>
    [HttpPost("{id:int}/image")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> UploadImage(
        int id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["Image"] = new[] { "Please choose an image to upload." }
            }));
        }

        await using var stream = file.OpenReadStream();
        var updated = await _products.SetImageAsync(id, stream, file.FileName, ct);
        return Ok(updated);
    }

    /// <summary>Downloads every product the caller owns as an .xlsx workbook.</summary>
    [HttpGet("export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var workbook = await _products.ExportToExcelAsync(ct);
        var fileName = $"products-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(workbook, ExcelContentType, fileName);
    }

    /// <summary>A blank workbook with the headers the importer expects.</summary>
    [HttpGet("import-template")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult ImportTemplate()
    {
        var workbook = _excel.BuildImportTemplate();
        return File(workbook, ExcelContentType, "product-import-template.xlsx");
    }

    /// <summary>
    /// Bulk-creates products from an .xlsx upload. The import is all-or-nothing: a
    /// response with Succeeded = false means nothing was written.
    /// </summary>
    [HttpPost("import")]
    [RequestSizeLimit(MaxUploadBytes)]
    [ProducesResponseType(typeof(ExcelImportResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ExcelImportResultDto), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ExcelImportResultDto>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new ValidationProblemDetails(new Dictionary<string, string[]>
            {
                ["File"] = new[] { "Please choose a spreadsheet to upload." }
            }));
        }

        await using var stream = file.OpenReadStream();
        var result = await _products.ImportFromExcelAsync(stream, ct);

        // Row-level problems are a client error, but the body is the same shape either
        // way so the UI can render the error list without branching on status.
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }
}
