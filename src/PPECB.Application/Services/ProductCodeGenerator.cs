using PPECB.Application.Abstractions;
using PPECB.Application.Validation;

namespace PPECB.Application.Services;

/// <summary>
/// Produces the next product code for the current month in the format yyyyMM-###.
/// The sequence restarts each month and is scoped to the owner. Reading the current
/// maximum and writing the next value is not atomic, so a unique index backs this up
/// in the database and ProductService retries when two concurrent creates collide.
/// </summary>
public class ProductCodeGenerator : IProductCodeGenerator
{
    private readonly IProductRepository _products;
    private readonly IDateTimeProvider _clock;

    public ProductCodeGenerator(IProductRepository products, IDateTimeProvider clock)
    {
        _products = products;
        _clock = clock;
    }

    public async Task<string> GenerateAsync(CancellationToken ct = default)
    {
        var prefix = CodeFormats.BuildProductCodePrefix(_clock.UtcNow);
        var maxSequence = await _products.GetMaxSequenceForPrefixAsync(prefix, ct);
        return CodeFormats.FormatProductCode(prefix, maxSequence + 1);
    }
}