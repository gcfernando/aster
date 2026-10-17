using System.Numerics;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Expressions;
using ModernCalculator.Core.History;
using ModernCalculator.Core.Modes;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ReviewMathRegressionTests
{
    private static StandardCalculator CompletedAddition()
    {
        var calculator = new StandardCalculator();
        calculator.Digit('2'); calculator.Operator(BinaryOperation.Add);
        calculator.Digit('3'); calculator.Equals();
        return calculator;
    }

    [Fact]
    public void StandalonePercentDiscardsThePreviousRepeatedOperation()
    {
        var calculator = CompletedAddition();
        var events = 0;
        calculator.CalculationCompleted += _ => events++;
        calculator.Percent(); calculator.Equals(); calculator.Equals();
        Assert.Equal("0.05", calculator.Entry);
        Assert.Equal("Percent(5) =", calculator.Expression);
        Assert.Equal(0, events);
        calculator.Digit('7'); calculator.Equals();
        Assert.Equal("7", calculator.Entry);
        Assert.Empty(calculator.Expression);
    }

    [Fact]
    public void PercentSnapshotsRestoreBothTheOperandAndRepeatedEqualsState()
    {
        var calculator = CompletedAddition();
        var beforePercent = calculator.Snapshot();
        calculator.Percent();
        var afterPercent = calculator.Snapshot();
        calculator.Restore(beforePercent); calculator.Equals();
        Assert.Equal("8", calculator.Entry);
        calculator.Restore(afterPercent); calculator.Equals();
        Assert.Equal("0.05", calculator.Entry);
        Assert.Equal("Percent(5) =", calculator.Expression);
    }

    [Theory]
    [InlineData(BinaryOperation.Add, "220", "240")]
    [InlineData(BinaryOperation.Subtract, "180", "160")]
    [InlineData(BinaryOperation.Multiply, "20", "2")]
    [InlineData(BinaryOperation.Divide, "2000", "20000")]
    public void PendingPercentRetainsItsOperationAndRepeatedRightOperand(BinaryOperation operation, string first, string second)
    {
        var calculator = new StandardCalculator();
        calculator.SetValue(200m); calculator.Operator(operation);
        calculator.SetValue(10m); calculator.Percent(); calculator.Equals();
        Assert.Equal(first, calculator.Entry);
        calculator.Equals(); Assert.Equal(second, calculator.Entry);
    }

    [Theory]
    [InlineData("sin(18000000000000000)", 0)]
    [InlineData("cos(18000000000000000)", 1)]
    [InlineData("sin(1e20)", -0.984807753012208)]
    [InlineData("sin(-1e20)", 0.984807753012208)]
    public void LargeDegreeInputsRetainTheirPeriodicPhase(string expression, double expected)
    {
        var actual = new ExpressionEvaluator().Evaluate(expression);
        Assert.InRange(Math.Abs(actual - expected), 0, 1e-14);
    }

    [Theory]
    [InlineData("tan(9000000000000090)")]
    [InlineData("tan(-9000000000000090)")]
    public void LargeDegreeTangentPolesRemainDomainErrors(string expression) =>
        Assert.Throws<CalculationException>(() => new ExpressionEvaluator().Evaluate(expression));

    [Fact]
    public void DegreeReductionDoesNotChangeRadianOrNonperiodicFunctions()
    {
        var evaluator = new ExpressionEvaluator();
        const double input = 18000000000000000;
        Assert.Equal(Math.Sin(input), evaluator.Evaluate("sin(18000000000000000)", AngleUnit.Radians));
        Assert.Equal(90, evaluator.Evaluate("asin(1)"));
        Assert.Equal(Math.Asinh(1000), evaluator.Evaluate("asinh(1000)"));
    }

    [Fact]
    public void HistoryCanRestoreItsOwnItemsAndLazySearchResults()
    {
        var journal = new HistoryJournal();
        journal.Record("Standard", "1 + 2 =", "3");
        journal.Record("Scientific", "sqrt(16)", "4");
        var original = journal.Items.ToArray();
        journal.Restore(journal.Items);
        Assert.Equal(original, journal.Items);
        journal.Restore(journal.Search("sqrt"));
        Assert.Equal(original[0], Assert.Single(journal.Items));
    }

    [Fact]
    public void FailedHistoryEnumerationLeavesTheJournalIntact()
    {
        var journal = new HistoryJournal();
        journal.Record("Standard", "1 + 2 =", "3");
        var original = journal.Items.ToArray();
        Assert.Throws<InvalidOperationException>(() => journal.Restore(FailingEntries(original[0])));
        Assert.Equal(original, journal.Items);
    }

    [Theory]
    [InlineData("1E+100", "1", 100)]
    [InlineData("-1e100", "-1", 100)]
    [InlineData("1e1000", "1", 1000)]
    [InlineData("1200e-2", "12", 0)]
    [InlineData("-1200e-2", "-12", 0)]
    [InlineData("1.20e1", "12", 0)]
    [InlineData("0e-1000", "0", 0)]
    public void ProgrammerReusesExactWholeNumbersInExponentNotation(string input, string coefficient, int exponent)
    {
        var expected = BigInteger.Parse(coefficient) * BigInteger.Pow(10, exponent);
        var direct = new ProgrammerCalculator(); direct.SetValue(expected);
        var recalled = new ProgrammerCalculator(); recalled.ParseDecimalResult(input);
        Assert.Equal(direct.Bits, recalled.Bits);
        Assert.Equal(direct.Value, recalled.Value);
    }

    [Theory]
    [InlineData("1e-1")]
    [InlineData("1201e-2")]
    [InlineData("1e-1000")]
    [InlineData("1e1001")]
    [InlineData("1e-1001")]
    [InlineData("1e99999999999999999999")]
    [InlineData("1.00000000000000000000000000001")]
    public void InvalidProgrammerExponentReuseLeavesTheWordIntact(string input)
    {
        var calculator = new ProgrammerCalculator(); calculator.SetValue(42);
        Assert.Throws<CalculationException>(() => calculator.ParseDecimalResult(input));
        Assert.Equal(new BigInteger(42), calculator.Value);
    }

    [Fact]
    public void ProgrammerReuseStillLimitsRawInputLength()
    {
        var calculator = new ProgrammerCalculator(); calculator.SetValue(42);
        Assert.Throws<CalculationException>(() => calculator.ParseDecimalResult(new string('0', 128) + "1"));
        Assert.Equal(new BigInteger(42), calculator.Value);
    }

    [Fact]
    public void ProgrammerReusePreservesAcceptedGroupedInvariantDecimalInput()
    {
        var calculator = new ProgrammerCalculator();
        calculator.ParseDecimalResult("1,200.0");
        Assert.Equal(new BigInteger(1200), calculator.Value);
    }

    private static IEnumerable<HistoryItem> FailingEntries(HistoryItem first)
    {
        yield return first;
        throw new InvalidOperationException("Failed to read saved history.");
    }
}
