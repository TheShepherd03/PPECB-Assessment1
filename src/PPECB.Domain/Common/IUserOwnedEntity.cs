namespace PPECB.Domain.Common;

/// <summary>
/// Marks an entity as belonging to a single user. The DbContext applies a global
/// query filter over this property, so "each user only manages their own data" is
/// enforced in one place rather than being re-checked in every query.
/// </summary>
public interface IUserOwnedEntity
{
    string OwnerId { get; set; }
}
