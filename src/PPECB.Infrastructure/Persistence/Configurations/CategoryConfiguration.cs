using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PPECB.Domain.Entities;

namespace PPECB.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("Categories");

        builder.HasKey(c => c.CategoryId);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(c => c.CategoryCode)
            .IsRequired()
            .HasMaxLength(6)
            .IsUnicode(false);

        builder.Property(c => c.IsActive)
            .IsRequired();

        builder.Property(c => c.OwnerId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(c => c.UpdatedBy)
            .HasMaxLength(450);

        builder.Property(c => c.RowVersion)
            .IsRowVersion();

        // Category codes are unique per user. Global uniqueness would let one account's
        // codes block another's, which would leak the existence of other users' data.
        builder.HasIndex(c => new { c.OwnerId, c.CategoryCode })
            .IsUnique()
            .HasDatabaseName("IX_Categories_OwnerId_CategoryCode");

        builder.HasIndex(c => c.OwnerId)
            .HasDatabaseName("IX_Categories_OwnerId");
    }
}
