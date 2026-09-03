using FluentAssertions;
using Moq;
using PPECB.Application.Abstractions;
using PPECB.Application.DTOs;
using PPECB.Application.Services;
using PPECB.Domain.Entities;
using ValidationException = PPECB.Domain.Exceptions.ValidationException;

namespace PPECB.UnitTests;

public class CategoryServiceTests
{
    private readonly Mock<ICategoryRepository> _repository = new();
    private readonly Mock<IUnitOfWork> _unitOfWork = new();
    private readonly CategoryService _service;

    public CategoryServiceTests()
    {
        _service = new CategoryService(_repository.Object, _unitOfWork.Object);
    }

    [Theory]
    [InlineData("AB123")]
    [InlineData("ABCD123")]
    [InlineData("ABC12")]
    [InlineData("123ABC")]
    public async Task CreateAsync_rejects_a_badly_formatted_code(string code)
    {
        var act = () => _service.CreateAsync(new CreateCategoryDto { Name = "Fruit", CategoryCode = code });

        var error = await act.Should().ThrowAsync<ValidationException>();
        error.Which.Errors.Should().ContainKey(nameof(CreateCategoryDto.CategoryCode));

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_rejects_a_duplicate_code()
    {
        _repository
            .Setup(r => r.CodeExistsAsync("ABC123", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var act = () => _service.CreateAsync(new CreateCategoryDto { Name = "Fruit", CategoryCode = "ABC123" });

        var error = await act.Should().ThrowAsync<ValidationException>();
        error.Which.Errors[nameof(CreateCategoryDto.CategoryCode)]
            .Single().Should().Contain("already in use");

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_stores_the_code_upper_cased()
    {
        Category? saved = null;
        _repository
            .Setup(r => r.CodeExistsAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Category>(), It.IsAny<CancellationToken>()))
            .Callback<Category, CancellationToken>((c, _) => saved = c);

        var result = await _service.CreateAsync(new CreateCategoryDto
        {
            Name = "  Fruit  ",
            CategoryCode = "abc123",
            IsActive = true
        });

        saved.Should().NotBeNull();
        saved!.CategoryCode.Should().Be("ABC123");
        saved.Name.Should().Be("Fruit", "surrounding whitespace should be trimmed");
        result.CategoryCode.Should().Be("ABC123");

        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_excludes_the_category_being_edited_from_the_duplicate_check()
    {
        _repository
            .Setup(r => r.GetByIdAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Category { CategoryId = 7, Name = "Fruit", CategoryCode = "ABC123" });
        _repository
            .Setup(r => r.CodeExistsAsync("ABC123", 7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await _service.UpdateAsync(7, new UpdateCategoryDto
        {
            Name = "Fruit and veg",
            CategoryCode = "ABC123",
            IsActive = true
        });

        // Keeping its own code must not be reported as a duplicate of itself.
        _repository.Verify(r => r.CodeExistsAsync("ABC123", 7, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
