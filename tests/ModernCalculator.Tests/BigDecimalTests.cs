using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.History;
using ModernCalculator.Core.Memory;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class BigDecimalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(28)]
    [InlineData(29)]
    [InlineData(99)]
    [InlineData(100)]
    public void StandardAcceptsLongEntries(int digits)
    {
        var calculator = Type(new string('9', digits));
        Assert.Equal(new string('9', digits), calculator.Entry);
        Assert.Null(calculator.Error);
    }

    [Fact]
    public void EverySupportedInputLengthParticipatesInExactArithmetic()
    {
        for (var digits = 1; digits <= 100; digits++)
        {
            var calculator = Type(new string('9', digits));
            calculator.Operator(BinaryOperation.Add); calculator.Digit('1'); calculator.Equals();
            Assert.Null(calculator.Error);
            Assert.Equal("1" + new string('0', digits), calculator.Entry);
        }
    }

    [Fact]
    public void Digit101IsRejectedWithoutChangingEntry()
    {
        var calculator = Type(new string('9', 100));
        Assert.Throws<CalculationException>(() => calculator.Digit('1'));
        Assert.Equal(new string('9', 100), calculator.Entry);
    }

    [Fact]
    public void CarryAndRepeatedEqualsRemainExactBeyond100Digits()
    {
        var calculator = Type(new string('9', 100));
        calculator.Operator(BinaryOperation.Add); calculator.Digit('1'); calculator.Equals();
        Assert.Equal("1" + new string('0', 100), calculator.Entry);
        calculator.Equals();
        Assert.Equal("1" + new string('0', 99) + "1", calculator.Entry);
        calculator.Operator(BinaryOperation.Subtract); calculator.Digit('2'); calculator.Equals();
        Assert.Equal(new string('9', 100), calculator.Entry);
    }

    [Fact]
    public void MultiplicationRetainsEveryDigit()
    {
        var calculator = Type(new string('9', 100));
        calculator.Operator(BinaryOperation.Multiply);
        foreach (var digit in new string('9', 100)) calculator.Digit(digit);
        calculator.Equals();
        Assert.Null(calculator.Error);
        Assert.Equal(new string('9', 99) + "8" + new string('0', 99) + "1", calculator.Entry);
    }

    [Fact]
    public void FractionalAdditionAndPercentageRemainExact()
    {
        var fraction = "0." + new string('1', 100);
        var calculator = Type(fraction);
        calculator.Operator(BinaryOperation.Add); calculator.SetValue(BigDecimal.Parse(fraction)); calculator.Equals();
        Assert.Equal("0." + new string('2', 100), calculator.Entry);
        calculator.Clear(); calculator.SetValue(BigDecimal.Parse(new string('9', 100)));
        calculator.Operator(BinaryOperation.Add); calculator.Digit('1'); calculator.Percent(); calculator.Equals();
        var expected = BigDecimal.Parse(new string('9', 100)) * BigDecimal.Parse("1.01");
        Assert.Equal(expected, calculator.ExactValue);
    }

    [Theory]
    [InlineData("1", "3", "0.3333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333333")]
    [InlineData("2", "3", "0.6666666666666666666666666666666666666666666666666666666666666666666666666666666666666666666666666667")]
    [InlineData("1", "8", "0.125")]
    [InlineData("-1", "8", "-0.125")]
    public void DivisionUses100SignificantDigits(string left, string right, string expected) =>
        Assert.Equal(expected, (BigDecimal.Parse(left) / BigDecimal.Parse(right)).ToString());

    [Theory]
    [InlineData("1", "8", "0.12")]
    [InlineData("3", "8", "0.38")]
    [InlineData("-1", "8", "-0.12")]
    [InlineData("-3", "8", "-0.38")]
    [InlineData("1", "-8", "-0.12")]
    [InlineData("3", "-8", "-0.38")]
    [InlineData("-3", "-8", "0.38")]
    [InlineData("999", "100", "10")]
    public void DivisionRoundsTiesToEven(string left, string right, string expected) =>
        Assert.Equal(expected, BigDecimal.Divide(BigDecimal.Parse(left), BigDecimal.Parse(right), 2).ToString());

    [Theory]
    [InlineData("1.245", "1.24")]
    [InlineData("1.255", "1.26")]
    [InlineData("-1.245", "-1.24")]
    [InlineData("9.995", "10")]
    [InlineData("-9.995", "-10")]
    public void DisplayRoundingUsesEvenTies(string input, string expected) =>
        Assert.Equal(expected, BigDecimal.Parse(input).Round(3).ToString());

    [Theory]
    [InlineData("4", "2")]
    [InlineData("0.0004", "0.02")]
    [InlineData("1e1000", "1e500")]
    [InlineData("1e-1000", "1e-500")]
    public void SquareRootHandlesExactAndExtremeValues(string input, string expected) =>
        Assert.Equal(BigDecimal.Parse(expected), BigDecimal.SquareRoot(BigDecimal.Parse(input)));

    [Theory]
    [InlineData("1.5625", "1.2")]
    [InlineData("1.8225", "1.4")]
    public void SquareRootRoundsTiesToEven(string input, string expected) =>
        Assert.Equal(expected, BigDecimal.SquareRoot(BigDecimal.Parse(input), 2).ToString());

    [Fact]
    public void IrrationalRootHasCorrect100DigitRounding()
    {
        var root = BigDecimal.SquareRoot(BigDecimal.Parse("2"));
        Assert.Equal("1.414213562373095048801688724209698078569671875376948073176679737990732478462107038850387534327641573", root.ToString());
        Assert.Equal(100, root.SignificantDigits);
    }

    [Fact]
    public void NegativeOddExponentsUseCorrectRootScale()
    {
        var tenthRoot = BigDecimal.SquareRoot(BigDecimal.Parse("0.1"));
        Assert.Equal("0.3162277660168379331998893544432718533719555139325216826857504852792594438639238221344248108379300295", tenthRoot.ToString());
        Assert.Equal(tenthRoot / BigDecimal.Parse("10"), BigDecimal.SquareRoot(BigDecimal.Parse(".001")));
        Assert.Equal(tenthRoot * BigDecimal.Parse("1e-499"), BigDecimal.SquareRoot(BigDecimal.Parse("1e-999")));
    }

    [Fact]
    public void DomainErrorsAndBoundedResultsAreExplicit()
    {
        Assert.Throws<CalculationException>(() => { _ = BigDecimal.One / BigDecimal.Zero; });
        Assert.Throws<CalculationException>(() => BigDecimal.SquareRoot(BigDecimal.Parse("-1")));
        Assert.Throws<CalculationException>(() => { _ = BigDecimal.Parse("1e1000") * BigDecimal.Parse("10"); });
        Assert.Throws<CalculationException>(() => { _ = BigDecimal.Parse("1e-1000") / BigDecimal.Parse("10"); });
        Assert.Throws<CalculationException>(() => { _ = BigDecimal.Parse("1e1000") + BigDecimal.Parse("1e-1000"); });
        Assert.False(BigDecimal.TryParse("1e1001", out _));
        Assert.False(BigDecimal.TryParse("1e-1001", out _));
        Assert.False(BigDecimal.TryParse("1e999999999999999999", out _));
        Assert.False(BigDecimal.TryParse("1e-999999999999999999", out _));
    }

    [Fact]
    public void NormalizationEqualityAndOrderingAreConsistent()
    {
        var one = BigDecimal.Parse("1.0000");
        Assert.Equal(BigDecimal.One, one); Assert.Equal(BigDecimal.One.GetHashCode(), one.GetHashCode());
        Assert.True(BigDecimal.Parse("-1000") < BigDecimal.Parse("0.001"));
        Assert.Equal(BigDecimal.Zero, BigDecimal.Parse("-0.000"));
        Assert.Equal("12000", BigDecimal.Parse("1.2e4").ToString());
    }

    [Fact]
    public void CompatibilityConversionsNeverSilentlyNarrow()
    {
        Assert.True(BigDecimal.Parse("0.1").TryToDecimal(out var exact)); Assert.Equal(.1m, exact);
        Assert.False(BigDecimal.Parse("0." + new string('1', 100)).TryToDecimal(out _));
        Assert.False(BigDecimal.Parse("1e100").TryToDecimal(out _));
        Assert.False(BigDecimal.Parse("1e1000").TryToDouble(out _));
        Assert.False(BigDecimal.Parse("1e-1000").TryToDouble(out _));
        Assert.Throws<CalculationException>(() => BigDecimal.Parse("1e1000").ToDouble());
    }

    [Theory]
    [InlineData("en-US", "1,234.5", "1234.5")]
    [InlineData("de-DE", "1.234,5", "1234.5")]
    [InlineData("sv-SE", "\u221212,5", "-12.5")]
    [InlineData("sv-SE", "1.5", "1.5")]
    [InlineData("hi-IN", "12,34,567.8", "1234567.8")]
    public void ClipboardAndFormattingAreCultureAware(string cultureName, string input, string expected)
    {
        var culture = CultureInfo.GetCultureInfo(cultureName);
        Assert.True(NumberText.TryBigDecimal(input, culture, out var number));
        Assert.Equal(expected, number.ToString());
        Assert.True(NumberText.TryBigDecimal(NumberText.Format(number, culture), culture, out var roundTrip));
        Assert.Equal(number, roundTrip);
    }

    [Theory]
    [InlineData("~")]
    [InlineData("\u061c-")]
    [InlineData("\u200e\u2212")]
    [InlineData("negative:")]
    public void ExactClipboardSupportsCustomAndBidirectionalCultureSigns(string negativeSign)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("en-US").Clone();
        culture.NumberFormat.NegativeSign = negativeSign;
        var value = BigDecimal.Parse("-1.5");
        Assert.True(NumberText.TryBigDecimal(value.ToString(culture), culture, out var result));
        Assert.Equal(value, result);
        Assert.True(NumberText.TryBigDecimal(NumberText.Format(BigDecimal.Parse("-1234.5"), culture), culture, out result));
        Assert.Equal(BigDecimal.Parse("-1234.5"), result);
        Assert.True(NumberText.TryBigDecimal("1e" + negativeSign + "3", culture, out result));
        Assert.Equal(BigDecimal.Parse(".001"), result);
    }

    [Fact]
    public void IndianGroupingPreserves100DigitMantissa()
    {
        var culture = CultureInfo.GetCultureInfo("hi-IN");
        var value = BigDecimal.Parse(new string('9', 100));
        var grouped = NumberText.Format(value, culture);
        Assert.EndsWith(",99,99,999", grouped);
        Assert.Equal(49, grouped.Count(character => character == ','));
        Assert.True(NumberText.TryBigDecimal(grouped, culture, out var parsed));
        Assert.Equal(value, parsed);
    }

    [Theory]
    [InlineData("1,23")]
    [InlineData("1,234.5,6")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1/2")]
    [InlineData("1e1001")]
    [InlineData("1e-1001")]
    public void InvalidClipboardDoesNotParse(string input) =>
        Assert.False(NumberText.TryBigDecimal(input, CultureInfo.GetCultureInfo("en-US"), out _));

    [Theory]
    [InlineData("en-US", "1e1,000")]
    [InlineData("de-DE", "1e1.000")]
    [InlineData("en-US", "1e+1,000")]
    [InlineData("de-DE", "1e-1.000")]
    [InlineData("en-US", "1e1.5")]
    [InlineData("en-US", "1e1e2")]
    public void ExponentsRejectGroupingAndDecimalSeparators(string culture, string input) =>
        Assert.False(NumberText.TryBigDecimal(input, CultureInfo.GetCultureInfo(culture), out _));

    [Theory]
    [InlineData("en-US", "1,234.5e+2", "123450")]
    [InlineData("de-DE", "1.234,5e-2", "12.345")]
    public void GroupedMantissaWithExponentStillParses(string culture, string input, string expected)
    {
        Assert.True(NumberText.TryBigDecimal(input, CultureInfo.GetCultureInfo(culture), out var number));
        Assert.Equal(expected, number.ToString());
    }

    [Fact]
    public void TinyFixedPointValuesRoundTripThroughExactClipboard()
    {
        var tiny = BigDecimal.Parse("1e-1000");
        Assert.Equal(1002, tiny.ToString().Length);
        Assert.True(NumberText.TryBigDecimal(tiny.ToString(), CultureInfo.InvariantCulture, out var parsed));
        Assert.Equal(tiny, parsed);
        var preciseTiny = BigDecimal.Parse(new string('1', 100) + "e-1000");
        Assert.True(NumberText.TryBigDecimal(preciseTiny.ToString(CultureInfo.GetCultureInfo("de-DE")), CultureInfo.GetCultureInfo("de-DE"), out parsed));
        Assert.Equal(preciseTiny, parsed);
        Assert.False(NumberText.TryBigDecimal(new string('0', NumberText.MaximumExactInputLength + 1), CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void ClipboardEnforces100EnteredSignificantDigits()
    {
        Assert.True(NumberText.TryBigDecimal(new string('9', 100), CultureInfo.InvariantCulture, out _));
        Assert.False(NumberText.TryBigDecimal(new string('9', 101), CultureInfo.InvariantCulture, out _));
        Assert.False(NumberText.TryBigDecimal("1" + new string('0', 100), CultureInfo.InvariantCulture, out _));
        Assert.True(NumberText.TryBigDecimal("1e1000", CultureInfo.InvariantCulture, out _));
    }

    [Fact]
    public void MemoryAndHistoryPreserveLargeAndFractionalResults()
    {
        var value = BigDecimal.Parse(new string('9', 100)) * BigDecimal.Parse("1.01");
        var memory = new MemoryBank(); memory.Store(value); memory.Add(BigDecimal.Parse("0.01"));
        Assert.Equal(value + BigDecimal.Parse("0.01"), memory.RecallExact());
        Assert.Throws<CalculationException>(() => memory.Recall());
        Assert.Equal(memory.RecallExact(), memory.Items[0].ExactValue);
        var journal = new HistoryJournal(); journal.Record("Standard", "large result", memory.RecallExact().ToString());
        Assert.Equal(memory.RecallExact().ToString(CultureInfo.GetCultureInfo("sv-SE")), journal.Items[0].LocalizedResult(CultureInfo.GetCultureInfo("sv-SE")));
    }

    [Fact]
    public void ExactResultsCanBeRecalledAndContinuedWithoutInputLimitLoss()
    {
        var calculator = new StandardCalculator();
        var result = BigDecimal.Parse(new string('9', 100)) * BigDecimal.Parse(new string('9', 100));
        calculator.SetValue(result); calculator.Operator(BinaryOperation.Add); calculator.Digit('1'); calculator.Equals();
        Assert.Equal(result + BigDecimal.One, calculator.ExactValue);
    }

    private static StandardCalculator Type(string text)
    {
        var calculator = new StandardCalculator();
        foreach (var digit in text) { if (digit == '.') calculator.DecimalPoint(); else calculator.Digit(digit); }
        return calculator;
    }
}
