using FluentAssertions;
using Moq;
using PPECB.Application.Abstractions;
using PPECB.Application.Services;

namespace PPECB.UnitTests;

public class ProductCodeGeneratorTests
{
    private readonly Mock<IProductRepository> _products = new();
    private readonly Mock<IDateTimeProvider> _clock = new();
    private readonly ProductCodeGenerator _generator;

    public ProductCodeGeneratorTests()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2021, 5, 17, 10, 30, 0, DateTimeKind.Utc));
        _generator = new ProductCodeGenerator(_products.Object, _clock.Object);
    }

    [Fact]
    public async Task Generates_the_first_code_of_the_month_when_none_exist()
    {
        _products
            .Setup(p => p.GetMaxSequenceForPrefixAsync("202105", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        (await _generator.GenerateAsync()).Should().Be("202105-001");
    }

    [Fact]
    public async Task Continues_the_sequence_from_the_current_maximum()
    {
        _products
            .Setup(p => p.GetMaxSequenceForPrefixAsync("202105", It.IsAny<CancellationToken>()))
            .ReturnsAsync(22);

        // The brief's worked example: the 23rd product of May 2021.
        (await _generator.GenerateAsync()).Should().Be("202105-023");
    }

    [Fact]
    public async Task Grows_past_three_digits_rather_than_wrapping()
    {
        _products
            .Setup(p => p.GetMaxSequenceForPrefixAsync("202105", It.IsAny<CancellationToken>()))
            .ReturnsAsync(999);

        (await _generator.GenerateAsync()).Should().Be("202105-1000");
    }

    [Fact]
    public async Task Uses_the_prefix_for_the_month_the_product_is_created_in()
    {
        _clock.SetupGet(c => c.UtcNow).Returns(new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc));
        _products
            .Setup(p => p.GetMaxSequenceForPrefixAsync("202601", It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var code = await _generator.GenerateAsync();

        code.Should().StartWith("202601-");
        _products.Verify(
            p => p.GetMaxSequenceForPrefixAsync("202601", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
