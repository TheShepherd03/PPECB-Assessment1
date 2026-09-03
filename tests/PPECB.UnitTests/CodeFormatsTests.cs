using FluentAssertions;
using PPECB.Application.Validation;

namespace PPECB.UnitTests;

public class CodeFormatsTests
{
    [Theory]
    [InlineData("ABC123")]
    [InlineData("XYZ000")]
    [InlineData("abc123")] // case is normalised before storage, so lower case is accepted
    public void IsValidCategoryCode_accepts_three_letters_then_three_digits(string code) =>
        CodeFormats.IsValidCategoryCode(code).Should().BeTrue();

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("AB123")]     // only two letters
    [InlineData("ABCD123")]   // four letters
    [InlineData("ABC12")]     // only two digits
    [InlineData("ABC1234")]   // four digits
    [InlineData("A1C123")]    // digit inside the letter block
    [InlineData("ABC12A")]    // letter inside the digit block
    [InlineData("ABC 123")]   // embedded space
    [InlineData("ABC-123")]   // separator
    [InlineData(" ABC123 ")]  // untrimmed input is not silently accepted
    public void IsValidCategoryCode_rejects_anything_else(string? code) =>
        CodeFormats.IsValidCategoryCode(code).Should().BeFalse();

    [Fact]
    public void NormaliseCategoryCode_trims_and_upper_cases() =>
        CodeFormats.NormaliseCategoryCode("  abc123 ").Should().Be("ABC123");

    [Fact]
    public void BuildProductCodePrefix_uses_year_and_month() =>
        CodeFormats.BuildProductCodePrefix(new DateTime(2021, 5, 17)).Should().Be("202105");

    [Theory]
    [InlineData(1, "202105-001")]
    [InlineData(23, "202105-023")]   // the example given in the brief
    [InlineData(999, "202105-999")]
    [InlineData(1000, "202105-1000")] // rolls past three digits rather than wrapping
    public void FormatProductCode_pads_to_three_digits(int sequence, string expected) =>
        CodeFormats.FormatProductCode("202105", sequence).Should().Be(expected);

    [Theory]
    [InlineData("202105-023", 23)]
    [InlineData("202512-001", 1)]
    [InlineData("202105-1234", 1234)]
    public void TryGetSequence_reads_the_sequence(string code, int expected) =>
        CodeFormats.TryGetSequence(code).Should().Be(expected);

    [Theory]
    [InlineData("202113-001")] // month 13 does not exist
    [InlineData("202100-001")] // month 00 does not exist
    [InlineData("2021-05-001")]
    [InlineData("202105023")]
    [InlineData("not-a-code")]
    [InlineData(null)]
    public void TryGetSequence_returns_null_for_malformed_codes(string? code) =>
        CodeFormats.TryGetSequence(code).Should().BeNull();

    [Fact]
    public void Generated_codes_round_trip_through_the_parser()
    {
        var prefix = CodeFormats.BuildProductCodePrefix(new DateTime(2021, 5, 1));
        var code = CodeFormats.FormatProductCode(prefix, 23);

        CodeFormats.IsValidProductCode(code).Should().BeTrue();
        CodeFormats.TryGetSequence(code).Should().Be(23);
    }
}
