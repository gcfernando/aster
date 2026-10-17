using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Expressions;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class DeepMathRegressionTests
{
    [Theory]
    [InlineData(1.23456789123456e-28)]
    [InlineData(1.23456789123456e-27)]
    [InlineData(1.23456789123456e-20)]
    [InlineData(-1.23456789123456e-28)]
    [InlineData(1.23456789123456e-29)]
    public void FloatingDisplayPreservesTheRequestedSignificantDigits(double value)
    {
        foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.GetCultureInfo("sv-SE"), CultureInfo.GetCultureInfo("fa-IR") })
        foreach (var precision in new[] { 3, 8, 15 })
        foreach (var grouping in new[] { false, true })
        {
            var expected = double.Parse(value.ToString($"G{precision}", culture), NumberStyles.Float, culture);
            var text = NumberText.Format(value, culture, precision, grouping);
            Assert.True(NumberText.TryDouble(text, culture, out var actual), $"Could not parse {culture.Name}: {text}");
            Assert.Equal(expected, actual);
        }
    }

    [Theory]
    [InlineData("asin(5e-324)")]
    [InlineData("atan(5e-324)")]
    public void TinyInverseTrigonometryDoesNotUnderflowDuringDegreeConversion(string expression)
    {
        var evaluator = new ExpressionEvaluator();
        Assert.Equal(double.Epsilon * (180 / Math.PI), evaluator.Evaluate(expression));
        Assert.Equal(double.Epsilon, evaluator.Evaluate(expression, AngleUnit.Radians));
    }

    [Theory]
    [InlineData("sin(1e-321)")]
    [InlineData("tan(1e-321)")]
    public void TinyDegreeAnglesOnlyRoundAtTheFinalConversion(string expression)
    {
        Assert.Equal(1e-321 * (Math.PI / 180), new ExpressionEvaluator().Evaluate(expression));
    }

    [Theory]
    [InlineData("sin(5e-324)")]
    [InlineData("tan(5e-324)")]
    public void TrulyUnderflowingDegreeResultsAreRejected(string expression) =>
        Assert.Throws<CalculationException>(() => new ExpressionEvaluator().Evaluate(expression));

    [Fact]
    public void TinyCosineAndExactWholeTurnsRemainRepresentable()
    {
        var evaluator = new ExpressionEvaluator();
        Assert.Equal(1, evaluator.Evaluate("cos(5e-324)"));
        Assert.Equal(0, evaluator.Evaluate("sin(360)"));
        Assert.Equal(0, evaluator.Evaluate("tan(-720)"));
    }

    [Fact]
    public void FloatingClipboardRoundTripsAcrossAllSpecificCultures()
    {
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        foreach (var value in new[] { -1234.5, 1234.5, -1.23e-50, 1.23e50 })
        {
            var copied = value.ToString("G17", culture);
            Assert.True(NumberText.TryDouble(copied, culture, out var parsed, allowGrouping: false), $"Could not parse {culture.Name}: {copied}");
            Assert.Equal(value, parsed);
            var sharedSeparator = culture.NumberFormat.NumberGroupSeparator == culture.NumberFormat.NumberDecimalSeparator;
            var grouped = value is -1234.5 or 1234.5
                ? sharedSeparator ? NumberText.Format(value, culture, 15, true) : value.ToString("N1", culture)
                : copied;
            Assert.True(NumberText.TryDouble(grouped, culture, out parsed), $"Could not parse grouped {culture.Name}: {grouped}");
            Assert.Equal(value, parsed);
        }
    }

    [Fact]
    public void ExactDecimalClipboardRoundTripsGroupedNativeSignsAcrossAllSpecificCultures()
    {
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        foreach (var value in new[] { -1234.5m, 1234.5m, -.0000000000000000000000000001m })
        {
            var text = NumberText.Format(value, culture, 29, true);
            Assert.True(NumberText.TryDecimal(text, culture, out var parsed), $"Could not parse {culture.Name}: {text}");
            Assert.Equal(value, parsed);
        }
    }

    [Theory]
    [InlineData("~")]
    [InlineData("\u061c-")]
    [InlineData("\u200e\u2212")]
    [InlineData("negative:")]
    public void CustomNativeSignsWorkInMantissasAndExponents(string sign)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NegativeSign = sign;
        var text = sign + "1,250e" + sign + "2";
        Assert.True(NumberText.TryDouble(text, culture, out var real));
        Assert.Equal(-12.5, real);
        Assert.True(NumberText.TryDecimal(text, culture, out var number));
        Assert.Equal(-12.5m, number);
    }

    [Theory]
    [InlineData("sv-SE")]
    [InlineData("fa-IR")]
    [InlineData("fa-AF")]
    [InlineData("ar-SA")]
    public void ScientificExpressionsAcceptTheirCulturesCopiedExponentSigns(string name)
    {
        var culture = CultureInfo.GetCultureInfo(name);
        var evaluator = new ExpressionEvaluator();
        foreach (var value in new[] { -1.23e-50, 1.23e50, -1234.5 })
            Assert.Equal(value, evaluator.Evaluate(value.ToString("G17", culture), culture: culture));
        var nativeMinus = culture.NumberFormat.NegativeSign;
        Assert.Equal(-2, evaluator.Evaluate("1" + nativeMinus + "3", culture: culture));
        var separator = culture.NumberFormat.NumberDecimalSeparator == "," ? ";" : ",";
        Assert.Equal(2, evaluator.Evaluate($"root(8{separator}3)", culture: culture));
    }

    [Theory]
    [InlineData("en-US", "1e1,000")]
    [InlineData("de-DE", "1e1.000")]
    [InlineData("sv-SE", "1e− 3")]
    [InlineData("fa-IR", "1e\u200e\u2212 3")]
    public void ExponentNormalizationDoesNotAdmitGroupingOrEmbeddedSpaces(string cultureName, string text)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.False(NumberText.TryDouble(text, culture, out _));
        Assert.False(NumberText.TryDecimal(text, culture, out _));
        Assert.Throws<CalculationException>(() => new ExpressionEvaluator().Evaluate(text, culture: culture));
    }

    [Fact]
    public void GroupingStillRequiresExplicitPermissionAndValidGroups()
    {
        var culture = CultureInfo.GetCultureInfo("ar-SA");
        var valid = (-1234.5).ToString("N1", culture);
        Assert.False(NumberText.TryDouble(valid, culture, out _, allowGrouping: false));
        var invalid = culture.NumberFormat.NegativeSign + "12" + culture.NumberFormat.NumberGroupSeparator + "34";
        Assert.False(NumberText.TryDouble(invalid, culture, out _));
        Assert.False(NumberText.TryDecimal(invalid, culture, out _));
    }

    [Theory]
    [InlineData("mn-Mong-MN")]
    [InlineData("en-US")]
    public void SharedDecimalAndGroupSeparatorsUseUnambiguousUngroupedOutput(string cultureName)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        culture.NumberFormat.NumberGroupSeparator = culture.NumberFormat.NumberDecimalSeparator;
        const decimal value = -1234.5m;
        var expected = value.ToString("G29", culture);
        Assert.Equal(expected, NumberText.Format(value, culture, 29, true));
        Assert.Equal(expected, NumberText.Format((double)value, culture, 15, true));
        Assert.Equal(expected, NumberText.Format(BigDecimal.FromDecimal(value), culture, 100, true));
        Assert.True(NumberText.TryDecimal(expected, culture, out var exact)); Assert.Equal(value, exact);
        Assert.True(NumberText.TryDouble(expected, culture, out var real, allowGrouping: false)); Assert.Equal((double)value, real);
        Assert.True(NumberText.TryBigDecimal(expected, culture, out var big)); Assert.Equal(BigDecimal.FromDecimal(value), big);
    }

    [Theory]
    [InlineData("1,234,567")]
    [InlineData("-1,234,5")]
    [InlineData("1,234,567e2")]
    public void SharedSeparatorsRejectAmbiguousGroupingInsteadOfTryingAnotherCulture(string text)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NumberDecimalSeparator = culture.NumberFormat.NumberGroupSeparator = ",";
        Assert.False(NumberText.TryDecimal(text, culture, out _));
        Assert.False(NumberText.TryDouble(text, culture, out _));
        Assert.False(NumberText.TryBigDecimal(text, culture, out _));
    }

    [Fact]
    public void SharedSeparatorsRetainCulturePriorityForASingleDecimalAndClearInvariantInput()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NumberDecimalSeparator = culture.NumberFormat.NumberGroupSeparator = ",";
        foreach (var text in new[] { "1,234", "1.234" })
        {
            Assert.True(NumberText.TryDecimal(text, culture, out var exact)); Assert.Equal(1.234m, exact);
            Assert.True(NumberText.TryDouble(text, culture, out var real, allowGrouping: false)); Assert.Equal(1.234, real);
            Assert.True(NumberText.TryBigDecimal(text, culture, out var big)); Assert.Equal(BigDecimal.Parse("1.234"), big);
        }
    }

    [Fact]
    public void ClearEntryThenOperatorReplacementPreservesTheAccumulatorForEveryOperatorPair()
    {
        foreach (var previous in Enum.GetValues<BinaryOperation>())
        foreach (var replacement in Enum.GetValues<BinaryOperation>())
        {
            var calculator = new StandardCalculator(); calculator.SetValue(8m);
            calculator.Operator(previous); calculator.Digit('9'); calculator.ClearEntry();
            var cleared = calculator.Snapshot();
            calculator.Operator(replacement); calculator.Digit('2'); calculator.Equals();
            var expected = replacement switch
            {
                BinaryOperation.Add => "10", BinaryOperation.Subtract => "6",
                BinaryOperation.Multiply => "16", BinaryOperation.Divide => "4", _ => throw new InvalidOperationException()
            };
            Assert.Equal(expected, calculator.Entry);
            Assert.Null(calculator.Error);
            calculator.Restore(cleared); calculator.Equals();
            if (previous == BinaryOperation.Divide) Assert.NotNull(calculator.Error);
            else
            {
                Assert.Null(calculator.Error);
                Assert.Equal(previous == BinaryOperation.Multiply ? "0" : "8", calculator.Entry);
            }
        }
    }
}
