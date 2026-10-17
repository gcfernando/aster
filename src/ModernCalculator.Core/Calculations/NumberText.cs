// Developer ::> Gehan Fernando
using System.Globalization;
using System.Numerics;

namespace ModernCalculator.Core.Calculations;

public static class NumberText
{
    public const int MaximumInputLength = 256;
    public const int MaximumExactInputLength = 4096;

    // Count the entered mantissa, including deliberate trailing zeroes but excluding leading zeroes.
    public static int SignificantInputDigits(string invariant)
    {
        var exponent = invariant.IndexOfAny(['e', 'E']);
        var mantissa = exponent >= 0 ? invariant[..exponent] : invariant;
        return mantissa.TrimStart('+', '-').Replace(".", "", StringComparison.Ordinal).TrimStart('0').Length;
    }

    public static bool TryBigDecimal(string? text, CultureInfo culture, out BigDecimal value)
    {
        value = BigDecimal.Zero;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumExactInputLength) return false;
        var input = text.Trim();
        if (HasAmbiguousSeparators(input, culture.NumberFormat)) return false;
        return TryBigCulture(input, culture, out value) ||
            culture.NumberFormat.NumberDecimalSeparator != "." && TryBigCulture(input, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryBigCulture(string text, CultureInfo culture, out BigDecimal value)
    {
        value = BigDecimal.Zero;
        var format = culture.NumberFormat;
        text = NormalizeLeadingSign(text, format);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex >= 0 ? text[..exponentIndex] : text;
        var exponent = exponentIndex >= 0 ? NormalizeLeadingSign(text[(exponentIndex + 1)..], format) : "";
        if (exponentIndex >= 0 && (exponent.Length == 0 ||
            !int.TryParse(exponent, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))) return false;
        var decimalIndex = mantissa.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var integer = (decimalIndex >= 0 ? mantissa[..decimalIndex] : mantissa).TrimStart('+', '-');
        var fraction = decimalIndex >= 0 ? mantissa[(decimalIndex + format.NumberDecimalSeparator.Length)..] : "";
        var group = DistinctGroupSeparator(format);
        if (group.Length > 0 && (fraction.Contains(group, StringComparison.Ordinal) || !ValidGroups(integer, group, format.NumberGroupSizes))) return false;
        var normalized = group.Length > 0 ? mantissa.Replace(group, "", StringComparison.Ordinal) : mantissa;
        normalized = normalized.Replace(format.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        if (exponentIndex >= 0) normalized += "e" + exponent;
        // Check input precision before normalization can discard entered trailing zeros.
        return SignificantInputDigits(normalized) <= BigDecimal.Precision && BigDecimal.TryParse(normalized, out value);
    }

    private static string NormalizeLeadingSign(string text, NumberFormatInfo format)
    {
        if (format.NegativeSign.Length > 0 && text.StartsWith(format.NegativeSign, StringComparison.Ordinal))
            return "-" + text[format.NegativeSign.Length..];
        if (format.PositiveSign.Length > 0 && text.StartsWith(format.PositiveSign, StringComparison.Ordinal))
            return "+" + text[format.PositiveSign.Length..];
        return text.StartsWith('−') ? "-" + text[1..] : text;
    }

    private static string NormalizeNumericSigns(string text, NumberFormatInfo format)
    {
        text = NormalizeLeadingSign(text, format);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        return exponentIndex < 0 ? text : text[..(exponentIndex + 1)] + NormalizeLeadingSign(text[(exponentIndex + 1)..], format);
    }

    private static string DistinctGroupSeparator(NumberFormatInfo format) =>
        format.NumberGroupSeparator == format.NumberDecimalSeparator ? "" : format.NumberGroupSeparator;

    private static bool HasAmbiguousSeparators(string text, NumberFormatInfo format)
    {
        var separator = format.NumberDecimalSeparator;
        if (separator != format.NumberGroupSeparator) return false;
        var mantissa = NormalizeLeadingSign(text, format).Split('e', 'E')[0];
        var first = mantissa.IndexOf(separator, StringComparison.Ordinal);
        // A shared separator always means decimal here. Multiple occurrences
        // must not be reinterpreted as invariant grouping after a parse failure.
        return first >= 0 && mantissa.IndexOf(separator, first + separator.Length, StringComparison.Ordinal) >= 0;
    }

    private static string InvariantNumericText(string text, NumberFormatInfo format)
    {
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex >= 0 ? text[..exponentIndex] : text;
        var group = DistinctGroupSeparator(format);
        if (group.Length > 0) mantissa = mantissa.Replace(group, "", StringComparison.Ordinal);
        mantissa = mantissa.Replace(format.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        // Grouping is only valid in the mantissa; leave the exponent intact so
        // the invariant parser still rejects grouped or fractional exponents.
        return mantissa + (exponentIndex >= 0 ? text[exponentIndex..] : "");
    }

    public static bool TryDecimal(string? text, CultureInfo culture, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputLength) return false;
        var input = text.Trim();
        if (HasAmbiguousSeparators(input, culture.NumberFormat)) return false;
        if (TryCulture(input, culture, out value)) return true;
        // Pasted invariant numbers are accepted only after the user's culture has had priority.
        return culture.NumberFormat.NumberDecimalSeparator != "." &&
            TryCulture(input, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryCulture(string text, CultureInfo culture, out decimal value)
    {
        value = 0;
        var format = culture.NumberFormat;
        text = NormalizeNumericSigns(text, format);
        var exponentIndex = text.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex >= 0 ? text[..exponentIndex] : text;
        var decimalIndex = mantissa.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var integer = (decimalIndex >= 0 ? mantissa[..decimalIndex] : mantissa).TrimStart('+', '-');
        var fraction = decimalIndex >= 0 ? mantissa[(decimalIndex + format.NumberDecimalSeparator.Length)..] : "";
        var group = DistinctGroupSeparator(format);
        if (group.Length > 0 && (fraction.Contains(group, StringComparison.Ordinal) ||
            !ValidGroups(integer, group, format.NumberGroupSizes))) return false;
        var normalized = InvariantNumericText(text, format);
        var valid = decimal.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        // TryParse can round fractional digits. Compare exact base-10 coefficients before accepting input.
        return valid && IsExactDecimal(normalized, CultureInfo.InvariantCulture, value);
    }

    private static bool IsExactDecimal(string text, CultureInfo culture, decimal value)
    {
        var normalized = text;
        var grouping = culture.NumberFormat.NumberGroupSeparator;
        if (grouping.Length > 0) normalized = normalized.Replace(grouping, "", StringComparison.Ordinal);
        normalized = normalized.Replace(culture.NumberFormat.NumberDecimalSeparator, ".", StringComparison.Ordinal);
        var exponentIndex = normalized.IndexOfAny(['e', 'E']);
        var mantissa = exponentIndex >= 0 ? normalized[..exponentIndex] : normalized;
        var exponent = 0;
        if (exponentIndex >= 0 && !int.TryParse(normalized[(exponentIndex + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
        var decimalIndex = mantissa.IndexOf('.');
        var fractionDigits = decimalIndex >= 0 ? mantissa.Length - decimalIndex - 1 : 0;
        if (!BigInteger.TryParse(mantissa.Replace(".", "", StringComparison.Ordinal), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var inputCoefficient)) return false;
        if (inputCoefficient == 0) return value == 0;
        if (exponent is < -1000 or > 1000) return false;
        var bits = decimal.GetBits(value);
        var outputCoefficient = (BigInteger)(uint)bits[0] + ((BigInteger)(uint)bits[1] << 32) + ((BigInteger)(uint)bits[2] << 64);
        if (bits[3] < 0) outputCoefficient = -outputCoefficient;
        var difference = exponent - fractionDigits + ((bits[3] >> 16) & 0xFF);
        return difference >= 0 ? inputCoefficient * BigInteger.Pow(10, difference) == outputCoefficient
            : inputCoefficient == outputCoefficient * BigInteger.Pow(10, -difference);
    }

    public static bool TryDouble(string? text, CultureInfo culture, out double value, bool allowGrouping = true)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text) || text.Length > MaximumInputLength) return false;
        var input = text.Trim();
        if (HasAmbiguousSeparators(input, culture.NumberFormat)) return false;
        return TryFloatingCulture(input, culture, out value, allowGrouping) ||
            culture.NumberFormat.NumberDecimalSeparator != "." && TryFloatingCulture(input, CultureInfo.InvariantCulture, out value, allowGrouping);
    }

    private static bool TryFloatingCulture(string text, CultureInfo culture, out double value, bool allowGrouping)
    {
        value = 0;
        var format = culture.NumberFormat;
        text = NormalizeNumericSigns(text, format);
        var mantissa = text.Split('e', 'E')[0];
        var decimalIndex = mantissa.IndexOf(format.NumberDecimalSeparator, StringComparison.Ordinal);
        var integer = (decimalIndex >= 0 ? mantissa[..decimalIndex] : mantissa).TrimStart('+', '-');
        var fraction = decimalIndex >= 0 ? mantissa[(decimalIndex + format.NumberDecimalSeparator.Length)..] : "";
        var group = DistinctGroupSeparator(format);
        if (!allowGrouping && group.Length > 0 && mantissa.Contains(group, StringComparison.Ordinal)) return false;
        if (group.Length > 0 && (fraction.Contains(group, StringComparison.Ordinal) || !ValidGroups(integer, group, format.NumberGroupSizes))) return false;
        return double.TryParse(InvariantNumericText(text, format), NumberStyles.Float, CultureInfo.InvariantCulture, out value) && double.IsFinite(value) &&
            !(value == 0 && mantissa.Any(c => c is >= '1' and <= '9'));
    }

    private static bool ValidGroups(string integer, string separator, int[] sizes)
    {
        var groups = integer.Split(separator, StringSplitOptions.None);
        if (groups.Length == 1) return true;
        if (sizes.Length == 0 || sizes[0] == 0) return false;
        for (var i = groups.Length - 1; i >= 0; i--)
        {
            var size = sizes[Math.Min(groups.Length - 1 - i, sizes.Length - 1)];
            if (size == 0) return i == 0 && groups[0].Length > 0 && groups[0].All(char.IsAsciiDigit);
            if (!groups[i].All(char.IsAsciiDigit) || groups[i].Length == 0 ||
                (i > 0 ? groups[i].Length != size : groups[i].Length > size)) return false;
        }
        return true;
    }

    public static string Format(decimal value, CultureInfo culture, int precision = 12, bool grouping = true)
    {
        if (value == 0) return "0";
        var result = value.ToString($"G{Math.Clamp(precision, 3, 29)}", culture);
        if (!grouping || DistinctGroupSeparator(culture.NumberFormat).Length == 0 || result.Contains('E', StringComparison.OrdinalIgnoreCase)) return result;
        var separator = culture.NumberFormat.NumberDecimalSeparator;
        var fraction = result.Contains(separator, StringComparison.Ordinal) ? result.Split(separator)[1].Length : 0;
        return decimal.Parse(result, NumberStyles.Float, culture).ToString($"N{fraction}", culture);
    }

    public static string Format(BigDecimal value, CultureInfo culture, int precision = 100, bool grouping = true)
    {
        var text = value.Round(Math.Clamp(precision, 1, BigDecimal.MaximumCoefficientDigits)).ToString(culture);
        if (!grouping || DistinctGroupSeparator(culture.NumberFormat).Length == 0) return text;
        var negative = text.StartsWith(culture.NumberFormat.NegativeSign, StringComparison.Ordinal);
        var sign = negative ? culture.NumberFormat.NegativeSign : "";
        if (negative) text = text[sign.Length..];
        var point = text.IndexOf(culture.NumberFormat.NumberDecimalSeparator, StringComparison.Ordinal);
        var integer = point >= 0 ? text[..point] : text;
        var fraction = point >= 0 ? text[point..] : "";
        var sizes = culture.NumberFormat.NumberGroupSizes;
        if (sizes.Length == 0 || sizes[0] == 0) return sign + text;
        var pieces = new List<string>();
        var index = integer.Length;
        var sizeIndex = 0;
        while (index > 0)
        {
            var size = sizes[Math.Min(sizeIndex, sizes.Length - 1)];
            if (size == 0) { pieces.Add(integer[..index]); break; }
            var start = Math.Max(0, index - size);
            pieces.Add(integer[start..index]); index = start; sizeIndex++;
        }
        pieces.Reverse();
        return sign + string.Join(culture.NumberFormat.NumberGroupSeparator, pieces) + fraction;
    }

    public static string Format(double value, CultureInfo culture, int precision = 12, bool grouping = true)
    {
        if (!double.IsFinite(value)) throw new CalculationException("The result is outside the supported range.");
        if (value == 0) return "0";
        var text = value.ToString($"G{Math.Clamp(precision, 3, 15)}", culture);
        return TryDecimal(text, culture, out var number) && number != 0
            ? Format(number, culture, precision, grouping) : text;
    }
}
