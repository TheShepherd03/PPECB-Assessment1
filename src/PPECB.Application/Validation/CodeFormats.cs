using System.Text.RegularExpressions;

namespace PPECB.Application.Validation;

/// <summary>
/// The two code formats the brief specifies, kept in one place so the API, the Excel
/// importer and the unit tests all enforce exactly the same rule.
/// </summary>
public static partial class CodeFormats
{
    public const string CategoryCodeDescription = "Category code must be 3 letters followed by 3 numbers, for example ABC123.";

    /// <summary>Exactly three ASCII letters followed by exactly three digits.</summary>
    [GeneratedRegex(@"^[A-Za-z]{3}[0-9]{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex CategoryCodeRegex();

    /// <summary>yyyyMM followed by a hyphen and a 3+ digit sequence, e.g. 202105-023.</summary>
    [GeneratedRegex(@"^(?<year>\d{4})(?<month>0[1-9]|1[0-2])-(?<sequence>\d{3,})$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductCodeRegex();

    public static bool IsValidCategoryCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && CategoryCodeRegex().IsMatch(code);

    public static bool IsValidProductCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ProductCodeRegex().IsMatch(code);

    /// <summary>Codes are compared and stored upper-cased so "abc123" cannot duplicate "ABC123".</summary>
    public static string NormaliseCategoryCode(string? code) =>
        (code ?? string.Empty).Trim().ToUpperInvariant();

    /// <summary>Builds the yyyyMM prefix used to group product codes by month.</summary>
    public static string BuildProductCodePrefix(DateTime utcNow) =>
        utcNow.ToString("yyyyMM", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Formats a sequence into a full product code, padding to at least 3 digits.</summary>
    public static string FormatProductCode(string prefix, int sequence) =>
        $"{prefix}-{sequence.ToString("D3", System.Globalization.CultureInfo.InvariantCulture)}";

    /// <summary>
    /// Extracts the numeric sequence from a product code, or null when it does not match.
    /// </summary>
    public static int? TryGetSequence(string? productCode)
    {
        if (string.IsNullOrWhiteSpace(productCode)) return null;
        var match = ProductCodeRegex().Match(productCode);
        if (!match.Success) return null;
        return int.TryParse(match.Groups["sequence"].Value, out var sequence) ? sequence : null;
    }
}
