// Developer ::> Gehan Fernando
using System.Globalization;
using System.Numerics;

namespace ModernCalculator.Core.Calculations;

/// <summary>
/// Bounded base-10 arithmetic. Addition, subtraction and multiplication are exact;
/// division and roots round to 100 significant digits, using midpoint-to-even.
/// Normalized coefficients have at most 2,000 digits, scales are -1,000..2,000,
/// and nonzero values have an adjusted decimal exponent between -1,000 and 1,000.
/// </summary>
public readonly struct BigDecimal : IEquatable<BigDecimal>, IComparable<BigDecimal>
{
    public const int Precision = 100;
    public const int MaximumCoefficientDigits = 2000;
    public const int MaximumExponent = 1000;
    public BigInteger Coefficient { get; }
    public int Scale { get; }
    public int Sign => Coefficient.Sign;
    public int SignificantDigits => Digits(Coefficient);
    public static BigDecimal Zero => default;
    public static BigDecimal One => new(BigInteger.One, 0);

    public BigDecimal(BigInteger coefficient, int scale)
    {
        if (coefficient.IsZero) { Coefficient = BigInteger.Zero; Scale = 0; return; }
        if (scale is < -4000 or > 4000) throw RangeError();
        // Remove trailing coefficient zeros before enforcing the normalized range.
        while (coefficient % 10 == 0) { coefficient /= 10; scale--; }
        var digits = Digits(coefficient);
        var exponent = (long)digits - scale - 1;
        if (digits > MaximumCoefficientDigits || scale is < -1000 or > 2000 || exponent is < -MaximumExponent or > MaximumExponent)
            throw RangeError();
        Coefficient = coefficient; Scale = scale;
    }

    private static CalculationException RangeError() => new("The result exceeds the supported range (decimal exponent -1000 to +1000; 2000 coefficient digits).");
    private static int Digits(BigInteger number) => BigInteger.Abs(number).ToString(CultureInfo.InvariantCulture).Length;
    private static BigInteger Power(int exponent) => BigInteger.Pow(10, exponent);
    public static BigDecimal FromDecimal(decimal value) => Parse(value.ToString(CultureInfo.InvariantCulture));

    public static BigDecimal Parse(string text)
    {
        if (!TryParse(text, out var value)) throw new CalculationException("Enter a valid number within the supported decimal range.");
        return value;
    }

    public static bool TryParse(string? text, out BigDecimal value)
    {
        value = Zero;
        if (string.IsNullOrWhiteSpace(text) || text.Length > 4096) return false;
        var input = text.Trim();
        var exponentIndex = input.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex >= 0 ? input[..exponentIndex] : input;
        var exponent = 0;
        if (exponentIndex >= 0 && (!int.TryParse(input[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || exponent is < -1000 or > 1000)) return false;
        var negative = mantissa.StartsWith('-');
        if (mantissa.StartsWith('-') || mantissa.StartsWith('+')) mantissa = mantissa[1..];
        var point = mantissa.IndexOf('.');
        if (point >= 0 && mantissa.IndexOf('.', point + 1) >= 0) return false;
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal);
        if (digits.Length == 0 || !digits.All(char.IsAsciiDigit)) return false;
        var scale = (point >= 0 ? mantissa.Length - point - 1 : 0) - exponent;
        try
        {
            value = new(BigInteger.Parse((negative ? "-" : "") + digits, CultureInfo.InvariantCulture), scale);
            return true;
        }
        catch (CalculationException) { return false; }
    }

    public override string ToString() => ToString(CultureInfo.InvariantCulture);
    public string ToString(CultureInfo culture)
    {
        if (Sign == 0) return "0";
        var digits = BigInteger.Abs(Coefficient).ToString(CultureInfo.InvariantCulture);
        var point = digits.Length - Scale;
        var text = Scale <= 0 ? digits + new string('0', -Scale)
            : point > 0 ? digits.Insert(point, culture.NumberFormat.NumberDecimalSeparator)
            : "0" + culture.NumberFormat.NumberDecimalSeparator + new string('0', -point) + digits;
        return Sign < 0 ? culture.NumberFormat.NegativeSign + text : text;
    }

    public bool TryToDecimal(out decimal value)
    {
        if (decimal.TryParse(ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && FromDecimal(value) == this) return true;
        value = 0; return false;
    }
    public bool TryToDouble(out double value)
    {
        if (double.TryParse(ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) && (value != 0 || Sign == 0)) return true;
        value = 0; return false;
    }
    public double ToDouble() => TryToDouble(out var value) ? value : throw new CalculationException("This value is outside the floating-point range.");

    public static BigDecimal operator +(BigDecimal left, BigDecimal right)
    {
        var scale = Math.Max(left.Scale, right.Scale);
        return new(left.Coefficient * Power(scale - left.Scale) + right.Coefficient * Power(scale - right.Scale), scale);
    }
    public static BigDecimal operator -(BigDecimal left, BigDecimal right) => left + -right;
    public static BigDecimal operator -(BigDecimal value) => new(-value.Coefficient, value.Scale);
    public static BigDecimal operator *(BigDecimal left, BigDecimal right) => new(left.Coefficient * right.Coefficient, left.Scale + right.Scale);
    public static BigDecimal operator /(BigDecimal left, BigDecimal right) => Divide(left, right);

    public static BigDecimal Divide(BigDecimal left, BigDecimal right, int precision = Precision)
    {
        if (right.Sign == 0) throw new CalculationException("Cannot divide by zero.");
        if (left.Sign == 0) return Zero;
        if (precision is < 1 or > Precision) throw new ArgumentOutOfRangeException(nameof(precision));
        var numerator = BigInteger.Abs(left.Coefficient);
        var denominator = BigInteger.Abs(right.Coefficient);
        var ratioExponent = Digits(numerator) - Digits(denominator);
        if (ratioExponent >= 0 ? numerator < denominator * Power(ratioExponent) : numerator * Power(-ratioExponent) < denominator) ratioExponent--;
        var exponent = ratioExponent + right.Scale - left.Scale;
        var resultScale = precision - 1 - exponent;
        var shift = resultScale + right.Scale - left.Scale;
        if (shift >= 0) numerator *= Power(shift); else denominator *= Power(-shift);
        var quotient = BigInteger.DivRem(numerator, denominator, out var remainder);
        // Compare twice the remainder to round midpoint ties toward an even quotient.
        var comparison = (remainder * 2).CompareTo(denominator);
        if (comparison > 0 || comparison == 0 && !quotient.IsEven) quotient++;
        return new(left.Sign * right.Sign * quotient, resultScale);
    }

    public BigDecimal Round(int precision = Precision)
    {
        if (precision is < 1 or > MaximumCoefficientDigits) throw new ArgumentOutOfRangeException(nameof(precision));
        var remove = SignificantDigits - precision;
        if (remove <= 0 || Sign == 0) return this;
        var divisor = Power(remove);
        var rounded = BigInteger.DivRem(BigInteger.Abs(Coefficient), divisor, out var remainder);
        var comparison = (remainder * 2).CompareTo(divisor);
        if (comparison > 0 || comparison == 0 && !rounded.IsEven) rounded++;
        return new(Sign * rounded, Scale - remove);
    }

    public static BigDecimal SquareRoot(BigDecimal value, int precision = Precision)
    {
        if (value.Sign < 0) throw new CalculationException("A real square root requires a non-negative number.");
        if (value.Sign == 0) return Zero;
        if (precision is < 1 or > Precision) throw new ArgumentOutOfRangeException(nameof(precision));
        var exponent = value.SignificantDigits - value.Scale - 1;
        var rootExponent = (int)Math.Floor(exponent / 2d);
        var scale = precision - 1 - rootExponent;
        var shift = 2 * scale - value.Scale;
        var numerator = value.Coefficient;
        var denominator = BigInteger.One;
        if (shift >= 0) numerator *= Power(shift); else denominator = Power(-shift);
        var root = IntegerSquareRoot(numerator / denominator);
        // Compare the squared midpoint exactly so root rounding avoids floating-point error.
        var comparison = (4 * numerator).CompareTo(denominator * BigInteger.Pow(2 * root + 1, 2));
        if (comparison > 0 || comparison == 0 && !root.IsEven) root++;
        return new(root, scale);
    }

    private static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.IsZero) return value;
        var estimate = BigInteger.One << (int)((value.GetBitLength() + 1) / 2);
        while (true)
        {
            var next = (estimate + value / estimate) / 2;
            if (next >= estimate) return estimate;
            estimate = next;
        }
    }

    public int CompareTo(BigDecimal other)
    {
        var scale = Math.Max(Scale, other.Scale);
        return (Coefficient * Power(scale - Scale)).CompareTo(other.Coefficient * Power(scale - other.Scale));
    }
    public bool Equals(BigDecimal other) => Coefficient == other.Coefficient && Scale == other.Scale;
    public override bool Equals(object? obj) => obj is BigDecimal other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Coefficient, Scale);
    public static bool operator ==(BigDecimal left, BigDecimal right) => left.Equals(right);
    public static bool operator !=(BigDecimal left, BigDecimal right) => !left.Equals(right);
    public static bool operator <(BigDecimal left, BigDecimal right) => left.CompareTo(right) < 0;
    public static bool operator >(BigDecimal left, BigDecimal right) => left.CompareTo(right) > 0;
    public static bool operator <=(BigDecimal left, BigDecimal right) => left.CompareTo(right) <= 0;
    public static bool operator >=(BigDecimal left, BigDecimal right) => left.CompareTo(right) >= 0;
}
