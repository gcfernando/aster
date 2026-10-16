// Developer ::> Gehan Fernando
using ModernCalculator.Core.Calculations;

namespace ModernCalculator.Core.Conversions;

public sealed record UnitDefinition(string Name, string Symbol, double Scale, double Offset = 0)
{
    public override string ToString() => $"{Name} ({Symbol})";
}
public sealed record UnitCategory(string Name, IReadOnlyList<UnitDefinition> Units)
{
    public override string ToString() => Name;
}

public static class UnitConverter
{
    private static UnitDefinition U(string name, string symbol, double scale, double offset = 0) => new(name, symbol, scale, offset);
    public static IReadOnlyList<UnitCategory> Categories { get; } = Array.AsReadOnly<UnitCategory>([
        new("Length", Array.AsReadOnly<UnitDefinition>([
            U("Metre", "m", 1), U("Kilometre", "km", 1000), U("Centimetre", "cm", .01), U("Millimetre", "mm", .001),
            U("Inch", "in", .0254), U("Foot", "ft", .3048), U("Yard", "yd", .9144), U("Mile", "mi", 1609.344), U("Nautical mile", "nmi", 1852)])),
        new("Mass", Array.AsReadOnly<UnitDefinition>([
            U("Kilogram", "kg", 1), U("Gram", "g", .001), U("Milligram", "mg", .000001), U("Tonne", "t", 1000),
            U("Pound", "lb", .45359237), U("Ounce", "oz", .028349523125), U("Stone", "st", 6.35029318)])),
        new("Temperature", Array.AsReadOnly<UnitDefinition>([
            U("Celsius", "°C", 1, 273.15), U("Fahrenheit", "°F", 5d / 9, 273.15 - 32 * 5d / 9), U("Kelvin", "K", 1)])),
        new("Area", Array.AsReadOnly<UnitDefinition>([
            U("Square metre", "m²", 1), U("Square kilometre", "km²", 1e6), U("Square centimetre", "cm²", .0001),
            U("Hectare", "ha", 10000), U("Acre", "ac", 4046.8564224), U("Square foot", "ft²", .09290304), U("Square inch", "in²", .00064516)])),
        new("Volume", Array.AsReadOnly<UnitDefinition>([
            U("Litre", "L", .001), U("Millilitre", "mL", .000001), U("Cubic metre", "m³", 1),
            U("US gallon", "US gal", .003785411784), U("Imperial gallon", "imp gal", .00454609),
            U("US fluid ounce", "US fl oz", .0000295735295625), U("US cup", "US cup", .0002365882365)])),
        new("Time", Array.AsReadOnly<UnitDefinition>([
            U("Second", "s", 1), U("Millisecond", "ms", .001), U("Minute", "min", 60), U("Hour", "h", 3600),
            U("Day", "d", 86400), U("Week", "wk", 604800), U("Julian year (365.25 days)", "a", 31557600)])),
        new("Speed", Array.AsReadOnly<UnitDefinition>([
            U("Metre per second", "m/s", 1), U("Kilometre per hour", "km/h", 1d / 3.6), U("Mile per hour", "mph", .44704),
            U("Foot per second", "ft/s", .3048), U("Knot", "kn", 1852d / 3600)])),
        new("Data storage", Array.AsReadOnly<UnitDefinition>([
            U("Byte", "B", 1), U("Bit", "bit", .125), U("Kilobyte", "kB", 1000), U("Megabyte", "MB", 1e6), U("Gigabyte", "GB", 1e9), U("Terabyte", "TB", 1e12),
            U("Kibibyte", "KiB", 1024), U("Mebibyte", "MiB", 1048576), U("Gibibyte", "GiB", 1073741824), U("Tebibyte", "TiB", 1099511627776)])),
        new("Energy", Array.AsReadOnly<UnitDefinition>([
            U("Joule", "J", 1), U("Kilojoule", "kJ", 1000), U("Calorie (thermochemical)", "cal", 4.184),
            U("Kilocalorie", "kcal", 4184), U("Watt hour", "Wh", 3600), U("Kilowatt hour", "kWh", 3600000), U("BTU (IT)", "BTU", 1055.05585262)])),
        new("Power", Array.AsReadOnly<UnitDefinition>([
            U("Watt", "W", 1), U("Kilowatt", "kW", 1000), U("Megawatt", "MW", 1e6),
            U("Mechanical horsepower", "hp", 745.6998715822702), U("Metric horsepower", "PS", 735.49875)])),
        new("Pressure", Array.AsReadOnly<UnitDefinition>([
            U("Pascal", "Pa", 1), U("Kilopascal", "kPa", 1000), U("Megapascal", "MPa", 1e6), U("Bar", "bar", 100000),
            U("Atmosphere", "atm", 101325), U("Pound per square inch", "psi", 6894.757293168361), U("Millimetre of mercury", "mmHg", 133.322387415)])),
        new("Angles", Array.AsReadOnly<UnitDefinition>([
            U("Degree", "°", Math.PI / 180), U("Radian", "rad", 1), U("Gradian", "gon", Math.PI / 200), U("Turn", "turn", 2 * Math.PI)]))
    ]);

    public static double Convert(double value, UnitCategory category, UnitDefinition from, UnitDefinition to)
    {
        if (!double.IsFinite(value)) throw new CalculationException("Enter a finite number.");
        if (!category.Units.Contains(from) || !category.Units.Contains(to)) throw new CalculationException("Both units must belong to the selected category.");
        var basis = value * from.Scale + from.Offset;
        const double machineEpsilon = 2.2204460492503131e-16;
        var offsetTolerance = Math.Abs(from.Offset) * 4 * machineEpsilon;
        if (category.Name == "Temperature" && basis < -offsetTolerance) throw new CalculationException("Temperature cannot be below absolute zero.");
        if (category.Name == "Temperature" && basis < 0) basis = 0; // Cancel only offset round-off at absolute zero.
        // Validate temperature domains before the identity shortcut. Identity must not lose
        // small values through offset cancellation or large values through base-unit overflow.
        if (from == to) return value == 0 ? 0 : value;
        // Catalog scale ratios are finite and nonzero. Combining them first avoids a
        // base-unit intermediate outside binary64 when the final result is representable.
        var result = from.Offset == 0 && to.Offset == 0
            ? value * (from.Scale / to.Scale)
            : (basis - to.Offset) / to.Scale;
        if (!double.IsFinite(result)) throw new CalculationException("The conversion exceeds the supported range.");
        if (result == 0 && value != 0 && from.Offset == 0 && to.Offset == 0)
            throw new CalculationException("The conversion underflowed the supported range.");
        return result == 0 ? 0 : result;
    }
}
