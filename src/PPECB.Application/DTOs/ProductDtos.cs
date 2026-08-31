using System.ComponentModel.DataAnnotations;

namespace PPECB.Application.DTOs;

public class ProductDto
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryCode { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string? ImagePath { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? UpdatedDate { get; set; }
    public string? RowVersion { get; set; }
}

public class CreateProductDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Range(0.0, 9999999.99, ErrorMessage = "Price must be zero or greater.")]
    public decimal Price { get; set; }
}

public class UpdateProductDto
{
    [Required(ErrorMessage = "Product name is required.")]
    [StringLength(200, ErrorMessage = "Product name cannot exceed 200 characters.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000, ErrorMessage = "Description cannot exceed 1000 characters.")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Please select a category.")]
    public int CategoryId { get; set; }

    [Range(0.0, 9999999.99, ErrorMessage = "Price must be zero or greater.")]
    public decimal Price { get; set; }

    public string? RowVersion { get; set; }
}
