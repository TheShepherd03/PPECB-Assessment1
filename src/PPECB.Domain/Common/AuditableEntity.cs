namespace PPECB.Domain.Common;

/// <summary>
/// Base class for entities that track who created/changed them and when.
/// Populated automatically by the DbContext, never by callers.
/// </summary>
public abstract class AuditableEntity
{
    public string CreatedBy { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; }

    public string? UpdatedBy { get; set; }

    public DateTime? UpdatedDate { get; set; }

    /// <summary>
    /// SQL Server rowversion. EF Core uses this as a concurrency token so a second
    /// writer working from stale data fails instead of silently overwriting.
    /// </summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
