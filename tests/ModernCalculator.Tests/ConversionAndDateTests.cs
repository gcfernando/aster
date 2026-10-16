using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Conversions;
using ModernCalculator.Core.Modes;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ConversionAndDateTests
{
    [Theory]
    [InlineData("Length", "in", "cm", 1, 2.54)] [InlineData("Length", "mi", "km", 1, 1.609344)]
    [InlineData("Mass", "lb", "kg", 1, .45359237)] [InlineData("Temperature", "°C", "°F", 0, 32)]
    [InlineData("Temperature", "°F", "°C", 212, 100)] [InlineData("Temperature", "K", "°C", 0, -273.15)]
    [InlineData("Area", "ac", "m²", 1, 4046.8564224)] [InlineData("Volume", "US gal", "L", 1, 3.785411784)]
    [InlineData("Volume", "imp gal", "L", 1, 4.54609)] [InlineData("Time", "h", "s", 1, 3600)]
    [InlineData("Speed", "km/h", "m/s", 36, 10)] [InlineData("Data storage", "KiB", "B", 1, 1024)]
    [InlineData("Data storage", "MB", "B", 1, 1000000)] [InlineData("Energy", "kWh", "J", 1, 3600000)]
    [InlineData("Power", "kW", "W", 1, 1000)] [InlineData("Pressure", "atm", "Pa", 1, 101325)]
    [InlineData("Angles", "turn", "°", 1, 360)]
    public void ReferenceConversions(string categoryName, string source, string target, double input, double expected)
    {
        var category = UnitConverter.Categories.Single(item => item.Name == categoryName);
        ScientificTests.AssertClose(expected, UnitConverter.Convert(input, category, category.Units.Single(unit => unit.Symbol == source), category.Units.Single(unit => unit.Symbol == target)));
    }
    [Fact] public void EveryUnitRoundTrips()
    {
        Assert.Equal(12, UnitConverter.Categories.Count);
        foreach (var category in UnitConverter.Categories)
            foreach (var from in category.Units)
                foreach (var to in category.Units)
                {
                    var converted = UnitConverter.Convert(345.678, category, from, to);
                    ScientificTests.AssertClose(345.678, UnitConverter.Convert(converted, category, to, from));
                }
    }
    [Fact] public void AbsoluteZeroAndFiniteValues()
    {
        var temperature = UnitConverter.Categories.Single(item => item.Name == "Temperature");
        Assert.Throws<CalculationException>(() => UnitConverter.Convert(-274, temperature, temperature.Units[0], temperature.Units[2]));
        Assert.Throws<CalculationException>(() => UnitConverter.Convert(double.PositiveInfinity, temperature, temperature.Units[0], temperature.Units[2]));
        Assert.Equal(0, UnitConverter.Convert(-273.15, temperature, temperature.Units[0], temperature.Units[2]));
        Assert.Throws<CalculationException>(() => UnitConverter.Convert(-1e-15, temperature, temperature.Units[2], temperature.Units[0]));
    }
    [Fact] public void RejectsUnitsFromDifferentCategories()
    {
        var length = UnitConverter.Categories[0]; Assert.Throws<CalculationException>(() => UnitConverter.Convert(1, length, length.Units[0], UnitConverter.Categories[1].Units[0]));
    }
    [Theory]
    [InlineData(2024, 2, 28, 2024, 3, 1, 2)] [InlineData(2023, 2, 28, 2023, 3, 1, 1)]
    [InlineData(2024, 1, 1, 2024, 1, 1, 0)] [InlineData(2025, 1, 1, 2024, 12, 31, 1)]
    public void DateDifferences(int y1, int m1, int d1, int y2, int m2, int d2, int expected)
        => Assert.Equal(expected, DateCalculator.Difference(new(y1, m1, d1), new(y2, m2, d2)).TotalDays);
    [Fact] public void CalendarDifferenceCanBeReconstructed()
    {
        DateOnly start = new(2020, 2, 29); DateOnly end = new(2025, 3, 15);
        var difference = DateCalculator.Difference(start, end);
        Assert.Equal(end, start.AddYears(difference.Years).AddMonths(difference.Months).AddDays(difference.Days));
    }
    [Fact] public void EndOfMonthAndLeapYearClamping()
    {
        Assert.Equal(new(2023, 2, 28), DateCalculator.Add(new(2023, 1, 31), 0, 1, 0, 0));
        Assert.Equal(new(2025, 2, 28), DateCalculator.Add(new(2024, 2, 29), 1, 0, 0, 0));
        Assert.Equal(new(2024, 2, 29), DateCalculator.Add(new(2024, 3, 31), 0, -1, 0, 0));
        Assert.Equal(new(2024, 1, 16), DateCalculator.Add(new(2024, 1, 1), 0, 0, 2, 1));
    }
    [Fact] public void DateRangeBoundary() => Assert.Throws<ArgumentOutOfRangeException>(() => DateCalculator.Add(DateOnly.MaxValue, 0, 0, 0, 1));
}
