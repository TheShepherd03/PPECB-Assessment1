using FluentAssertions;
using Moq;
using PPECB.Application.Abstractions;
using PPECB.Application.DTOs;
using PPECB.Application.Services;
using PPECB.Domain.Entities;
using PPECB.Domain.Exceptions;
using ValidationException = PPECB.Domain.Exceptions.ValidationException;

namespace PPECB.UnitTests;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<ICategoryRepository> _categories = new();
    private readonly Mock<IProductCodeGenerator> _codeGenerator = new();
    private readonly Mock<IFileStorageService> _files = new();
    private readonly Mock<IExcelService> _excel = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly ProductService _service;

    public ProductServiceTests()
    {
        _service = new ProductService(
            _products.Object, _categories.Object, _codeGenerator.Object,
            _files.Object, _excel.Object, _unitOfWork.Object);
    }

    private void GivenActiveCategory(int id = 1) =>
        _categories
            .Setup(c => c.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { CategoryId = id, Name = "Fruit", CategoryCode = "ABC123", IsActive = true });

    [Fact]
    public async Task CreateAsync_assigns_the_generated_product_code()
    {
        GivenActiveCategory();
        Product? saved = null;
        _products
            .Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()))
            .Callback<Product, CancellationToken>((p, _) => saved = p);
        _codeGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("202105-023");

        var result = await _service.CreateAsync(new CreateProductDto
        {
            Name = "Apples", CategoryId = 1, Price = 19.99m
        });

        saved!.ProductCode.Should().Be("202105-023");
        result.ProductCode.Should().Be("202105-023");
        result.CategoryName.Should().Be("Fruit");
    }

    [Fact]
    public async Task CreateAsync_retries_with_a_new_code_when_another_write_takes_it_first()
    {
        GivenActiveCategory();
        _products.Setup(p => p.AddAsync(It.IsAny<Product>(), It.IsAny<CancellationToken>()));

        _codeGenerator
            .SetupSequence(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("202105-023")
            .ReturnsAsync("202105-024");

        // The first save loses the race; the second succeeds.
        var attempts = 0;
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                attempts++;
                if (attempts == 1) throw new DuplicateKeyException("duplicate");
                return Task.FromResult(1);
            });

        var result = await _service.CreateAsync(new CreateProductDto
        {
            Name = "Apples", CategoryId = 1, Price = 19.99m
        });

        result.ProductCode.Should().Be("202105-024");
        attempts.Should().Be(2);
    }

    [Fact]
    public async Task CreateAsync_rejects_an_inactive_category()
    {
        _categories
            .Setup(c => c.GetByIdAsync(2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { CategoryId = 2, Name = "Retired", IsActive = false });

        var act = () => _service.CreateAsync(new CreateProductDto { Name = "X", CategoryId = 2, Price = 1m });

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey(nameof(CreateProductDto.CategoryId));
    }

    [Fact]
    public async Task CreateAsync_rejects_a_category_the_caller_does_not_own()
    {
        // The ownership query filter makes another user's category read as missing.
        _categories
            .Setup(c => c.GetByIdAsync(99, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Category?)null);

        var act = () => _service.CreateAsync(new CreateProductDto { Name = "X", CategoryId = 99, Price = 1m });

        (await act.Should().ThrowAsync<ValidationException>())
            .Which.Errors.Should().ContainKey(nameof(CreateProductDto.CategoryId));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_removes_the_image_only_after_the_row_is_gone()
    {
        _products
            .Setup(p => p.GetByIdAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Product { ProductId = 5, ImagePath = "/uploads/products/abc.png" });

        var sequence = new List<string>();
        _unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add("save"))
            .ReturnsAsync(1);
        _files
            .Setup(f => f.DeleteProductImage(It.IsAny<string>()))
            .Callback<string?>(_ => sequence.Add("delete-file"));

        await _service.DeleteAsync(5);

        sequence.Should().Equal("save", "delete-file");
    }

    [Fact]
    public async Task ImportFromExcelAsync_writes_nothing_when_any_row_is_invalid()
    {
        _excel
            .Setup(e => e.ParseProducts(It.IsAny<Stream>()))
            .Returns(new ExcelImportParseResult
            {
                Rows = new List<ExcelProductRow>
                {
                    new() { RowNumber = 2, Name = "Good", CategoryCode = "ABC123", Price = 10m },
                    new() { RowNumber = 3, Name = "Bad", CategoryCode = "ZZZ999", Price = 10m }
                }
            });

        _categories
            .Setup(c => c.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>
            {
                new() { CategoryId = 1, Name = "Fruit", CategoryCode = "ABC123", IsActive = true }
            });

        var result = await _service.ImportFromExcelAsync(new MemoryStream());

        result.Succeeded.Should().BeFalse();
        result.ProductsImported.Should().Be(0);
        result.Errors.Should().ContainSingle()
            .Which.RowNumber.Should().Be(3);

        // The valid row must not be written either — the import is all-or-nothing.
        _products.Verify(p => p.AddRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ImportFromExcelAsync_assigns_sequential_codes_to_every_row()
    {
        _excel
            .Setup(e => e.ParseProducts(It.IsAny<Stream>()))
            .Returns(new ExcelImportParseResult
            {
                Rows = new List<ExcelProductRow>
                {
                    new() { RowNumber = 2, Name = "A", CategoryCode = "ABC123", Price = 1m },
                    new() { RowNumber = 3, Name = "B", CategoryName = "Fruit", Price = 2m },
                    new() { RowNumber = 4, Name = "C", CategoryCode = "abc123", Price = 3m }
                }
            });

        _categories
            .Setup(c => c.GetActiveAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Category>
            {
                new() { CategoryId = 1, Name = "Fruit", CategoryCode = "ABC123", IsActive = true }
            });

        _codeGenerator
            .Setup(g => g.GenerateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("202105-005");

        List<Product>? staged = null;
        _products
            .Setup(p => p.AddRangeAsync(It.IsAny<IEnumerable<Product>>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<Product>, CancellationToken>((p, _) => staged = p.ToList());

        var result = await _service.ImportFromExcelAsync(new MemoryStream());

        result.Succeeded.Should().BeTrue();
        result.ProductsImported.Should().Be(3);
        staged!.Select(p => p.ProductCode)
            .Should().Equal("202105-005", "202105-006", "202105-007");

        // Rows resolve by code or by name, and codes are matched case-insensitively.
        staged.Should().OnlyContain(p => p.CategoryId == 1);
    }
}
