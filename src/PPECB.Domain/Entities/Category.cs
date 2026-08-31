using PPECB.Domain.Common;

namespace PPECB.Domain.Entities;

public class Category : AuditableEntity, IUserOwnedEntity
{
    public int CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Unique code in the form of 3 letters followed by 3 digits, e.g. ABC123.
    /// Stored upper-cased; uniqueness is scoped per owner.
    /// </summary>
    public string CategoryCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string OwnerId { get; set; } = string.Empty;

    public ICollection<Product> Products { get; set; } = new List<Product>();
}
