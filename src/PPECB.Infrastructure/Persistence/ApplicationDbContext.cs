using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PPECB.Application.Abstractions;
using PPECB.Domain.Common;
using PPECB.Domain.Entities;
using PPECB.Domain.Exceptions;
using PPECB.Infrastructure.Identity;

namespace PPECB.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>SQL Server error numbers for unique index / primary key violations.</summary>
    private const int UniqueIndexViolation = 2601;
    private const int UniqueConstraintViolation = 2627;

    private readonly ICurrentUserService _currentUser;
    private readonly IDateTimeProvider _clock;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUser,
        IDateTimeProvider clock)
        : base(options)
    {
        _currentUser = currentUser;
        _clock = clock;
    }

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Product> Products => Set<Product>();

    /// <summary>
    /// Read by the global query filters. Exposed as a context property (rather than
    /// capturing the service in the lambda) so EF Core treats it as a per-query
    /// parameter instead of baking one user's id into the cached model.
    /// </summary>
    public string CurrentOwnerId => _currentUser.UserId ?? string.Empty;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // "Each user must only manage their own data" enforced once, centrally, rather
        // than relying on every query remembering to filter.
        builder.Entity<Category>().HasQueryFilter(c => c.OwnerId == CurrentOwnerId);
        builder.Entity<Product>().HasQueryFilter(p => p.OwnerId == CurrentOwnerId);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditAndOwnership();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            var entityName = ex.Entries.FirstOrDefault()?.Entity.GetType().Name ?? "record";
            throw new ConcurrencyConflictException(entityName);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new DuplicateKeyException(
                "That value is already in use. Please use a different code.");
        }
    }

    /// <summary>
    /// Stamps audit fields and assigns ownership. Doing this here means no service can
    /// forget to, and callers cannot spoof CreatedBy by putting it in a request body.
    /// </summary>
    private void ApplyAuditAndOwnership()
    {
        var userId = _currentUser.UserId ?? string.Empty;
        var now = _clock.UtcNow;

        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedBy = userId;
                    entry.Entity.CreatedDate = now;

                    if (entry.Entity is IUserOwnedEntity owned && string.IsNullOrEmpty(owned.OwnerId))
                    {
                        owned.OwnerId = userId;
                    }
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedBy = userId;
                    entry.Entity.UpdatedDate = now;

                    // Creation details and ownership are immutable after insert.
                    entry.Property(nameof(AuditableEntity.CreatedBy)).IsModified = false;
                    entry.Property(nameof(AuditableEntity.CreatedDate)).IsModified = false;

                    if (entry.Entity is IUserOwnedEntity)
                    {
                        entry.Property(nameof(IUserOwnedEntity.OwnerId)).IsModified = false;
                    }
                    break;
            }
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException sql &&
        sql.Number is UniqueIndexViolation or UniqueConstraintViolation;
}
