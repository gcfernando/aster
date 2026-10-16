using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Conversions;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ConverterAuditTests
{
    [Theory]
    [InlineData("en-US", "1.25", 1.25)]
    [InlineData("sv-SE", "1,25", 1.25)]
    [InlineData("de-DE", "-2,5e2", -250)]
    [InlineData("sv-SE", "1.25", 1.25)]
    public void RegionalFloatingEntryIsAcceptedWithoutGrouping(string cultureName, string input, double expected)
    {
        Assert.True(NumberText.TryDouble(input, CultureInfo.GetCultureInfo(cultureName), out var actual, allowGrouping: false));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("en-US", "1,234")]
    [InlineData("sv-SE", "1 234")]
    [InlineData("en-US", "1e999")]
    [InlineData("en-US", "1e-999")]
    [InlineData("en-US", "NaN")]
    public void InvalidOrOutOfRangeEntryIsRejected(string cultureName, string input)
        => Assert.False(NumberText.TryDouble(input, CultureInfo.GetCultureInfo(cultureName), out _, allowGrouping: false));

    [Fact]
    public void VeryLargeAndSmallValuesRemainConvertibleAndReversible()
    {
        foreach (var category in UnitConverter.Categories.Where(item => item.Name != "Temperature"))
        foreach (var source in category.Units)
        foreach (var target in category.Units)
        foreach (var input in new[] { 1e-200, 1e200 })
        {
            var result = UnitConverter.Convert(input, category, source, target);
            Assert.True(double.IsFinite(result) && result != 0);
            var restored = UnitConverter.Convert(result, category, target, source);
            Assert.InRange(Math.Abs(restored / input - 1), 0, 1e-12);
        }
    }

    [Fact]
    public void ScaleAndOffsetExplanationMatchesTheConversionModel()
    {
        foreach (var category in UnitConverter.Categories)
        foreach (var source in category.Units)
        foreach (var target in category.Units)
        {
            const double input = 345.678;
            var multiplier = source.Scale / target.Scale;
            var offset = (source.Offset - target.Offset) / target.Scale;
            var explained = input * multiplier + offset;
            var result = UnitConverter.Convert(input, category, source, target);
            Assert.InRange(Math.Abs(explained - result), 0, Math.Max(1e-12, Math.Abs(result) * 1e-12));
        }
    }

    [Fact]
    public void CatalogUnitNamesAndSymbolsAreUniqueWithinEachCategory()
    {
        Assert.Equal(12, UnitConverter.Categories.Count);
        foreach (var category in UnitConverter.Categories)
        {
            Assert.Equal(category.Units.Count, category.Units.Select(item => item.Name).Distinct(StringComparer.Ordinal).Count());
            Assert.Equal(category.Units.Count, category.Units.Select(item => item.Symbol).Distinct(StringComparer.Ordinal).Count());
        }
    }
}
