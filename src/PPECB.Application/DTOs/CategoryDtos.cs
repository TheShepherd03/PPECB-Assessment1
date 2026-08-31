using System.ComponentModel.DataAnnotations;

namespace PPECB.Application.DTOs;

public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }

    /// <summary>Base64 rowversion, round-tripped by the client for concurrency checks.</summary>
    public string? RowVersion { get; set; }
}

/// <summary>Slim shape for the product form's category dropdown.</summary>
public class CategoryLookupDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
}

public class CreateCategoryDto
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category code is required.")]
    public string CategoryCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public class UpdateCategoryDto
{
    [Required(ErrorMessage = "Category name is required.")]
    [StringLength(100, ErrorMessage = "Category name cannot exceed 100 characters.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Category code is required.")]
    public string CategoryCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    /// <summary>Base64 rowversion from the record the user loaded. Enables optimistic concurrency.</summary>
    public string? RowVersion { get; set; }
}
