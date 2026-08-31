using PPECB.Domain.Common;

namespace PPECB.Domain.Entities;

public class Product : AuditableEntity, IUserOwnedEntity
{
    public int ProductId { get; set; }

    /// <summary>
    /// Auto-generated on create in the form yyyyMM-###, e.g. 202105-023.
    /// Never supplied by the caller.
    /// </summary>
    public string ProductCode { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// The brief lists "CategoryName" on the product. It is modelled as a foreign key
    /// to Category so the name cannot drift out of sync; the category name is projected
    /// onto the product DTOs for display and Excel export.
    /// </summary>
    public int CategoryId { get; set; }

    public Category? Category { get; set; }

    public decimal Price { get; set; }

    /// <summary>
    /// Web-relative path of the uploaded image, e.g. /uploads/products/{guid}.png.
    /// Null when no image has been uploaded.
    /// </summary>
    public string? ImagePath { get; set; }

    public string OwnerId { get; set; } = string.Empty;
}
