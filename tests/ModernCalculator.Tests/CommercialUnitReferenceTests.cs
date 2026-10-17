using ModernCalculator.Core.Conversions;
using Xunit;

namespace ModernCalculator.Tests;

/// <summary>
/// Independent definitions, rather than production-factor round trips:
/// NIST SP811 Appendix B.8 (international foot/pound, US/Imperial volume,
/// thermochemical calorie, IT Btu, standard gravity and conventional mmHg):
/// https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b8
/// BIPM SI brochure (SI prefixes, Celsius, accepted non-SI units and angles):
/// https://www.bipm.org/en/publications/si-brochure
/// NIST/IEC binary prefixes: https://physics.nist.gov/cuu/Units/binary.html
/// Derived quantities below use those definitions, not UnitDefinition.Scale.
/// </summary>
public sealed class CommercialUnitReferenceTests
{
    public static IEnumerable<object[]> UnitAnchors()
    {
        const decimal inch = .0254m;
        const decimal foot = 12 * inch;
        const decimal pound = .45359237m;
        const decimal standardGravity = 9.80665m;
        const decimal usGallon = 231 * inch * inch * inch;
        const double pi = 3.141592653589793238462643383279502884;
        static object[] A(string category, string symbol, decimal scale, decimal offset = 0) => [category, symbol, (double)scale, (double)offset];
        static object[] Angle(string symbol, double scale) => ["Angles", symbol, scale, 0d];
        return [
            A("Length", "m", 1), A("Length", "km", 1000), A("Length", "cm", .01m), A("Length", "mm", .001m),
            A("Length", "in", inch), A("Length", "ft", foot), A("Length", "yd", 3 * foot),
            A("Length", "mi", 5280 * foot), A("Length", "nmi", 1852),
            A("Mass", "kg", 1), A("Mass", "g", .001m), A("Mass", "mg", .000001m), A("Mass", "t", 1000),
            A("Mass", "lb", pound), A("Mass", "oz", pound / 16), A("Mass", "st", 14 * pound),
            A("Temperature", "°C", 1, 273.15m), A("Temperature", "°F", 5m / 9, 273.15m - 32 * 5m / 9), A("Temperature", "K", 1),
            A("Area", "m²", 1), A("Area", "km²", 1000 * 1000), A("Area", "cm²", .01m * .01m),
            A("Area", "ha", 100 * 100), A("Area", "ac", 43560 * foot * foot),
            A("Area", "ft²", foot * foot), A("Area", "in²", inch * inch),
            A("Volume", "L", .001m), A("Volume", "mL", .000001m), A("Volume", "m³", 1),
            A("Volume", "US gal", usGallon), A("Volume", "imp gal", .00454609m),
            A("Volume", "US fl oz", usGallon / 128), A("Volume", "US cup", usGallon / 16),
            A("Time", "s", 1), A("Time", "ms", .001m), A("Time", "min", 60), A("Time", "h", 60 * 60),
            A("Time", "d", 24 * 60 * 60), A("Time", "wk", 7 * 24 * 60 * 60), A("Time", "a", 365.25m * 24 * 60 * 60),
            A("Speed", "m/s", 1), A("Speed", "km/h", 1000m / 3600), A("Speed", "mph", 5280 * foot / 3600),
            A("Speed", "ft/s", foot), A("Speed", "kn", 1852m / 3600),
            A("Data storage", "B", 1), A("Data storage", "bit", 1m / 8), A("Data storage", "kB", 1000),
            A("Data storage", "MB", 1000m * 1000), A("Data storage", "GB", 1000m * 1000 * 1000), A("Data storage", "TB", 1000m * 1000 * 1000 * 1000),
            A("Data storage", "KiB", 1L << 10), A("Data storage", "MiB", 1L << 20),
            A("Data storage", "GiB", 1L << 30), A("Data storage", "TiB", 1L << 40),
            A("Energy", "J", 1), A("Energy", "kJ", 1000), A("Energy", "cal", 4.184m), A("Energy", "kcal", 1000 * 4.184m),
            A("Energy", "Wh", 3600), A("Energy", "kWh", 1000 * 3600), A("Energy", "BTU", 453.59237m * 4.1868m * 5 / 9),
            A("Power", "W", 1), A("Power", "kW", 1000), A("Power", "MW", 1000 * 1000),
            A("Power", "hp", 550 * foot * pound * standardGravity), A("Power", "PS", 75 * standardGravity),
            A("Pressure", "Pa", 1), A("Pressure", "kPa", 1000), A("Pressure", "MPa", 1000 * 1000),
            A("Pressure", "bar", 100000), A("Pressure", "atm", 101325),
            A("Pressure", "psi", pound * standardGravity / (inch * inch)),
            A("Pressure", "mmHg", 13595.1m * standardGravity * .001m),
            Angle("°", pi / 180), Angle("rad", 1), Angle("gon", pi / 200), Angle("turn", 2 * pi)
        ];
    }

    [Theory]
    [MemberData(nameof(UnitAnchors))]
    public void CatalogDefinitionsAndCanonicalConversionsMatchIndependentReferences(string categoryName, string symbol, double scale, double offset)
    {
        var category = Assert.Single(UnitConverter.Categories, item => item.Name == categoryName);
        var unit = Assert.Single(category.Units, item => item.Symbol == symbol);
        AssertWithinTwoUlps(scale, unit.Scale);
        AssertWithinTwoUlps(offset, unit.Offset);
        var canonicalSymbol = categoryName switch
        {
            "Length" => "m", "Mass" => "kg", "Temperature" => "K", "Area" => "m²",
            "Volume" => "m³", "Time" => "s", "Speed" => "m/s", "Data storage" => "B",
            "Energy" => "J", "Power" => "W", "Pressure" => "Pa", "Angles" => "rad",
            _ => throw new InvalidOperationException("Missing independent canonical unit.")
        };
        var canonical = Assert.Single(category.Units, item => item.Symbol == canonicalSymbol);
        var expected = scale + offset;
        AssertWithinTwoUlps(expected, UnitConverter.Convert(1, category, unit, canonical));
        // Supply the independent reference value, rather than the preceding
        // production conversion result, to test the inverse affine operation.
        var restored = UnitConverter.Convert(expected, category, canonical, unit);
        Assert.InRange(Math.Abs(restored - 1), 0, categoryName == "Temperature" ? 1e-12 : 1e-15);
    }

    [Fact]
    public void EveryCatalogUnitHasExactlyOneIndependentReferenceAnchor()
    {
        var anchors = UnitAnchors().Select(row => ((string)row[0], (string)row[1])).ToArray();
        var catalog = UnitConverter.Categories.SelectMany(category => category.Units.Select(unit => (category.Name, unit.Symbol))).ToArray();
        Assert.Equal(78, anchors.Length);
        Assert.Equal(anchors.Length, anchors.Distinct().Count());
        Assert.Equal(anchors.Length, catalog.Length);
        Assert.True(anchors.ToHashSet().SetEquals(catalog), "Update independent references when adding or changing catalog units.");
    }

    [Theory]
    [InlineData("°C", "°F", 0, 32)]
    [InlineData("°C", "°F", 100, 212)]
    [InlineData("°F", "°C", 32, 0)]
    [InlineData("°F", "°C", 212, 100)]
    [InlineData("K", "°C", 0, -273.15)]
    [InlineData("°F", "K", -459.67, 0)]
    public void AffineTemperatureAnchorsDistinguishOffsetsFromTemperatureIntervals(string source, string target, double input, double expected)
    {
        var category = Assert.Single(UnitConverter.Categories, item => item.Name == "Temperature");
        var from = Assert.Single(category.Units, item => item.Symbol == source);
        var to = Assert.Single(category.Units, item => item.Symbol == target);
        Assert.InRange(Math.Abs(UnitConverter.Convert(input, category, from, to) - expected), 0, 1e-12);
    }

    private static void AssertWithinTwoUlps(double expected, double actual) =>
        Assert.InRange(actual, Math.BitDecrement(Math.BitDecrement(expected)), Math.BitIncrement(Math.BitIncrement(expected)));
}
