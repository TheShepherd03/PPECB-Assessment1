using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using PPECB.Application.Abstractions;
using PPECB.Domain.Entities;
using PPECB.Infrastructure.Persistence;
using PPECB.Infrastructure.Persistence.Repositories;

namespace PPECB.UnitTests;

/// <summary>
/// The central security guarantee of the application: one user can never read or reach
/// another user's rows. These run against the real DbContext (in-memory provider) so the
/// global query filters and audit stamping are genuinely exercised.
/// </summary>
public class OwnershipIsolationTests
{
    private sealed class StubUser : ICurrentUserService
    {
        public string? UserId { get; set; }
        public string? Email => "test@example.com";
        public bool IsAuthenticated => UserId is not null;
        public string RequireUserId() => UserId ?? throw new UnauthorizedAccessException();
    }

    private sealed class FixedClock : IDateTimeProvider
    {
        public DateTime UtcNow { get; set; } = new(2021, 5, 17, 10, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>
    /// Every context must be built through this one path. EF Core caches an internal
    /// service provider per distinct options configuration, so a builder configured even
    /// slightly differently would resolve to a *separate* in-memory store under the same
    /// database name, and the contexts would not see each other's data.
    /// </summary>
    private static ApplicationDbContext CreateContext(
        string databaseName, ICurrentUserService user, IDateTimeProvider? clock = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            // The in-memory provider has no rowversion; suppress the expected warning.
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics
                .InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        return new ApplicationDbContext(options, user, clock ?? new FixedClock());
    }

    [Fact]
    public async Task A_user_only_sees_their_own_categories()
    {
        var databaseName = Guid.NewGuid().ToString();

        var alice = new StubUser { UserId = "alice" };
        await using (var context = CreateContext(databaseName, alice))
        {
            context.Categories.Add(new Category { Name = "Alice fruit", CategoryCode = "AAA111", IsActive = true });
            await context.SaveChangesAsync();
        }

        var bob = new StubUser { UserId = "bob" };
        await using (var context = CreateContext(databaseName, bob))
        {
            context.Categories.Add(new Category { Name = "Bob veg", CategoryCode = "BBB222", IsActive = true });
            await context.SaveChangesAsync();

            var visible = await context.Categories.ToListAsync();
            visible.Should().ContainSingle().Which.Name.Should().Be("Bob veg");
        }

        await using (var context = CreateContext(databaseName, alice))
        {
            var visible = await context.Categories.ToListAsync();
            visible.Should().ContainSingle().Which.Name.Should().Be("Alice fruit");
        }
    }

    [Fact]
    public async Task Fetching_another_users_category_by_id_returns_nothing()
    {
        var databaseName = Guid.NewGuid().ToString();
        int aliceCategoryId;

        var alice = new StubUser { UserId = "alice" };
        await using (var context = CreateContext(databaseName, alice))
        {
            var category = new Category { Name = "Alice fruit", CategoryCode = "AAA111", IsActive = true };
            context.Categories.Add(category);
            await context.SaveChangesAsync();
            aliceCategoryId = category.CategoryId;
        }

        var bob = new StubUser { UserId = "bob" };
        await using (var context = CreateContext(databaseName, bob))
        {
            var repository = new CategoryRepository(context);

            // Guessing another user's id must not be a way in.
            var result = await repository.GetByIdAsync(aliceCategoryId);
            result.Should().BeNull();
        }
    }

    [Fact]
    public async Task Ownership_and_audit_fields_are_stamped_on_insert()
    {
        var databaseName = Guid.NewGuid().ToString();
        var alice = new StubUser { UserId = "alice" };

        await using var context = CreateContext(databaseName, alice);
        var category = new Category { Name = "Fruit", CategoryCode = "AAA111", IsActive = true };
        context.Categories.Add(category);
        await context.SaveChangesAsync();

        category.OwnerId.Should().Be("alice");
        category.CreatedBy.Should().Be("alice");
        category.CreatedDate.Should().Be(new DateTime(2021, 5, 17, 10, 0, 0, DateTimeKind.Utc));
        category.UpdatedBy.Should().BeNull();
        category.UpdatedDate.Should().BeNull();
    }

    [Fact]
    public async Task Updating_stamps_the_updated_fields_and_leaves_creation_details_alone()
    {
        var databaseName = Guid.NewGuid().ToString();
        var alice = new StubUser { UserId = "alice" };

        await using (var context = CreateContext(databaseName, alice))
        {
            context.Categories.Add(new Category { Name = "Fruit", CategoryCode = "AAA111", IsActive = true });
            await context.SaveChangesAsync();
        }

        var laterClock = new FixedClock { UtcNow = new DateTime(2021, 6, 1, 8, 0, 0, DateTimeKind.Utc) };
        await using (var context = CreateContext(databaseName, alice, laterClock))
        {
            var category = await context.Categories.SingleAsync();
            category.Name = "Fruit and veg";
            await context.SaveChangesAsync();

            category.CreatedBy.Should().Be("alice");
            category.CreatedDate.Should().Be(new DateTime(2021, 5, 17, 10, 0, 0, DateTimeKind.Utc));
            category.UpdatedBy.Should().Be("alice");
            category.UpdatedDate.Should().Be(new DateTime(2021, 6, 1, 8, 0, 0, DateTimeKind.Utc));
        }
    }
}
