using ModernCalculator.Core.Calculations;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class IndependentCalculationTests
{
    private static StandardCalculator CompletedAddition()
    {
        var calculator = new StandardCalculator(); calculator.Digit('2');
        calculator.Operator(BinaryOperation.Add); calculator.Digit('3'); calculator.Equals();
        return calculator;
    }

    [Fact]
    public void ResultSignChangesKeepAnAccurateTraceWithoutRepeatingOrRecordingTheOldOperation()
    {
        var calculator = CompletedAddition(); var newHistory = 0;
        calculator.CalculationCompleted += _ => newHistory++;
        calculator.Unary(UnaryOperation.Negate);
        Assert.Equal("-5", calculator.Entry); Assert.Equal("Negate(5) =", calculator.Expression);
        calculator.Equals(); Assert.Equal("-5", calculator.Entry);
        calculator.Unary(UnaryOperation.Negate);
        Assert.Equal("5", calculator.Entry); Assert.Equal("Negate(-5) =", calculator.Expression);
        Assert.Equal(0, newHistory);
    }

    [Theory]
    [InlineData("digit", "-52")]
    [InlineData("decimal", "-5.")]
    [InlineData("backspace", "0")]
    [InlineData("clear-entry", "0")]
    [InlineData("recall", "7")]
    public void EditingASignChangedResultDiscardsItsCompletedTrace(string action, string expectedEntry)
    {
        var calculator = CompletedAddition(); calculator.Unary(UnaryOperation.Negate);
        switch (action)
        {
            case "digit": calculator.Digit('2'); break;
            case "decimal": calculator.DecimalPoint(); break;
            case "backspace": calculator.Backspace(); break;
            case "clear-entry": calculator.ClearEntry(); break;
            case "recall": calculator.SetValue(7m); break;
        }
        Assert.Equal(expectedEntry, calculator.Entry); Assert.Empty(calculator.Expression); Assert.Null(calculator.Error);
    }

    [Fact]
    public void PercentageAfterAResultDescribesTheTransformedValueAndContinuesNormally()
    {
        var calculator = CompletedAddition(); calculator.Percent();
        Assert.Equal("0.05", calculator.Entry); Assert.Equal("Percent(5) =", calculator.Expression);
        calculator.Operator(BinaryOperation.Multiply); calculator.Digit('2'); calculator.Equals();
        Assert.Equal("0.1", calculator.Entry); Assert.Equal("0.05 × 2 =", calculator.Expression);
    }

    [Fact]
    public void RepeatedEqualsStillUsesTheCapturedOperand()
    {
        var calculator = CompletedAddition(); calculator.Equals(); calculator.Equals();
        Assert.Equal("11", calculator.Entry); Assert.Equal("8 + 3 =", calculator.Expression);
        calculator.Digit('7'); Assert.Equal("7", calculator.Entry); Assert.Empty(calculator.Expression);
    }

    [Fact]
    public void PendingUnaryOperandStillParticipatesInImmediateChaining()
    {
        var calculator = new StandardCalculator(); calculator.Digit('5'); calculator.Operator(BinaryOperation.Add);
        calculator.Digit('3'); calculator.Unary(UnaryOperation.Square); calculator.Operator(BinaryOperation.Multiply);
        calculator.Digit('2'); calculator.Equals(); Assert.Equal("28", calculator.Entry); Assert.Null(calculator.Error);
    }
}
