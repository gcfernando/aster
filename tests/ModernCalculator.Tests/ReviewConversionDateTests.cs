using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Conversions;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ReviewConversionDateTests
{
    [Theory]
    [InlineData("Length", "km", 1e308)]
    [InlineData("Length", "mm", 1e-322)]
    [InlineData("Temperature", "°C", 1e-15)]
    [InlineData("Temperature", "°F", -459.67)]
    public void IdentityPreservesFiniteInput(string categoryName, string symbol, double value)
    {
        var category = Category(categoryName); var unit = Unit(category, symbol);
        Assert.Equal(value, UnitConverter.Convert(value, category, unit, unit));
    }

    [Theory]
    [InlineData("km", "mi", 1e308)]
    [InlineData("mm", "cm", 1e-320)]
    public void FiniteFinalResultsDoNotFailAtIntermediateBaseValue(string source, string target, double input)
    {
        var category = Category("Length"); var from = Unit(category, source); var to = Unit(category, target);
        var expected = input * (from.Scale / to.Scale);
        Assert.True(double.IsFinite(expected) && expected != 0);
        Assert.Equal(expected, UnitConverter.Convert(input, category, from, to));
    }

    [Theory]
    [InlineData("km", "mm", 1e308)]
    [InlineData("mm", "km", double.Epsilon)]
    public void UnrepresentableFinalResultsRemainErrors(string source, string target, double input)
    {
        var category = Category("Length");
        Assert.Throws<CalculationException>(() => UnitConverter.Convert(input, category, Unit(category, source), Unit(category, target)));
    }

    [Theory]
    [InlineData("°C", -274)]
    [InlineData("°F", -460)]
    [InlineData("K", -1e-15)]
    public void IdentityStillRejectsTemperaturesBelowAbsoluteZero(string symbol, double value)
    {
        var category = Category("Temperature"); var unit = Unit(category, symbol);
        Assert.Throws<CalculationException>(() => UnitConverter.Convert(value, category, unit, unit));
    }

    private static UnitCategory Category(string name) => UnitConverter.Categories.Single(category => category.Name == name);
    private static UnitDefinition Unit(UnitCategory category, string symbol) => category.Units.Single(unit => unit.Symbol == symbol);
}
