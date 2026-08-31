using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PPECB.Domain.Entities;

namespace PPECB.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products");

        builder.HasKey(p => p.ProductId);

        builder.Property(p => p.ProductCode)
            .IsRequired()
            .HasMaxLength(20)
            .IsUnicode(false);

        builder.Property(p => p.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasMaxLength(1000);

        // decimal(18,2) rather than the default, which would silently truncate cents.
        builder.Property(p => p.Price)
            .IsRequired()
            .HasPrecision(18, 2);

        builder.Property(p => p.ImagePath)
            .HasMaxLength(400);

        builder.Property(p => p.OwnerId)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.CreatedBy)
            .IsRequired()
            .HasMaxLength(450);

        builder.Property(p => p.UpdatedBy)
            .HasMaxLength(450);

        builder.Property(p => p.RowVersion)
            .IsRowVersion();

        // Backs the retry in ProductService: if two concurrent creates pick the same
        // code, the database rejects the loser rather than storing a duplicate.
        builder.HasIndex(p => new { p.OwnerId, p.ProductCode })
            .IsUnique()
            .HasDatabaseName("IX_Products_OwnerId_ProductCode");

        builder.HasIndex(p => p.CategoryId)
            .HasDatabaseName("IX_Products_CategoryId");

        builder.HasOne(p => p.Category)
            .WithMany(c => c.Products)
            .HasForeignKey(p => p.CategoryId)
            // A category holding products cannot be deleted out from under them.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
