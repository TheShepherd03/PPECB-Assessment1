using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PPECB.Application.Abstractions;
using PPECB.Application.Common;
using PPECB.Application.DTOs;

namespace PPECB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categories;

    public CategoriesController(ICategoryService categories) => _categories = categories;

    /// <summary>Returns a page of the caller's categories.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<CategoryDto>>> GetPaged(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = PagingDefaults.DefaultPageSize,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var page = await _categories.GetPagedAsync(pageNumber, pageSize, search, ct);
        return Ok(page);
    }

    /// <summary>Active categories only, for populating the product form's dropdown.</summary>
    [HttpGet("lookup")]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryLookupDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryLookupDto>>> GetLookup(CancellationToken ct)
    {
        var categories = await _categories.GetActiveLookupAsync(ct);
        return Ok(categories);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken ct)
    {
        var category = await _categories.GetByIdAsync(id, ct);
        return Ok(category);
    }

    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryDto request, CancellationToken ct)
    {
        var created = await _categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.CategoryId }, created);
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Update(
        int id, [FromBody] UpdateCategoryDto request, CancellationToken ct)
    {
        var updated = await _categories.UpdateAsync(id, request, ct);
        return Ok(updated);
    }
}
