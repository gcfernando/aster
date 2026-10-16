using System.Globalization;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Memory;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class StandardTests
{
    private static StandardCalculator Enter(string text)
    {
        var calculator = new StandardCalculator(); Type(calculator, text); return calculator;
    }
    private static void Type(StandardCalculator calculator, string text)
    {
        foreach (var character in text) { if (character == '.') calculator.DecimalPoint(); else calculator.Digit(character); }
    }
    [Theory]
    [InlineData("1.2", "2.3", BinaryOperation.Add, "3.5")]
    [InlineData("1", "3", BinaryOperation.Subtract, "-2")]
    [InlineData("1.25", "8", BinaryOperation.Multiply, "10")]
    [InlineData("10", "4", BinaryOperation.Divide, "2.5")]
    public void Arithmetic(string left, string right, BinaryOperation operation, string expected)
    {
        var calculator = Enter(left); calculator.Operator(operation); Type(calculator, right); calculator.Equals();
        Assert.Null(calculator.Error); Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), calculator.Value);
    }
    [Fact] public void DecimalAdditionIsExact()
    {
        var calculator = Enter("0.1"); calculator.Operator(BinaryOperation.Add); Type(calculator, "0.2"); calculator.Equals(); Assert.Equal(.3m, calculator.Value);
    }
    [Fact] public void ExecutesSequentially()
    {
        var calculator = Enter("2"); calculator.Operator(BinaryOperation.Add); Type(calculator, "3"); calculator.Operator(BinaryOperation.Multiply);
        Type(calculator, "4"); calculator.Equals(); Assert.Equal(20, calculator.Value);
    }
    [Fact] public void PendingOperatorIsReplaced()
    {
        var calculator = Enter("8"); calculator.Operator(BinaryOperation.Add); calculator.Operator(BinaryOperation.Multiply);
        Type(calculator, "3"); calculator.Equals(); Assert.Equal(24, calculator.Value);
    }
    [Fact] public void RepeatedEqualsAndNewCalculation()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Add); Type(calculator, "2"); calculator.Equals(); calculator.Equals(); calculator.Equals();
        Assert.Equal(11, calculator.Value); calculator.Digit('9'); Assert.Equal(9, calculator.Value); Assert.Empty(calculator.Expression);
    }
    [Fact] public void CanContinueAfterEquals()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Add); Type(calculator, "2"); calculator.Equals();
        calculator.Operator(BinaryOperation.Multiply); Type(calculator, "4"); calculator.Equals(); Assert.Equal(28, calculator.Value);
    }
    [Theory]
    [InlineData(BinaryOperation.Add, 220)] [InlineData(BinaryOperation.Subtract, 180)]
    [InlineData(BinaryOperation.Multiply, 20)] [InlineData(BinaryOperation.Divide, 2000)]
    public void PercentageSemantics(BinaryOperation operation, int expected)
    {
        var calculator = Enter("200"); calculator.Operator(operation); Type(calculator, "10"); calculator.Percent(); calculator.Equals(); Assert.Equal(expected, calculator.Value);
    }
    [Fact] public void StandalonePercentage() { var calculator = Enter("12"); calculator.Percent(); Assert.Equal(.12m, calculator.Value); }
    [Fact] public void EntryEditing()
    {
        var calculator = Enter("00012.3"); calculator.DecimalPoint(); calculator.Backspace(); Assert.Equal("12.", calculator.Entry);
        calculator.Digit('5'); Assert.Equal(12.5m, calculator.Value); calculator.ClearEntry(); Assert.Equal(0, calculator.Value);
        calculator.Clear(); Assert.Empty(calculator.Expression);
    }
    [Fact] public void ClearEntryPreservesPendingOperation()
    {
        var calculator = Enter("8"); calculator.Operator(BinaryOperation.Add); Type(calculator, "93"); calculator.ClearEntry(); Type(calculator, "2"); calculator.Equals(); Assert.Equal(10, calculator.Value);
    }
    [Fact] public void UnaryTransformsRightOperand()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Add); Type(calculator, "3"); calculator.Unary(UnaryOperation.Square); calculator.Equals(); Assert.Equal(14, calculator.Value);
    }
    [Fact] public void ANewDigitReplacesATransformedOperand()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Add); calculator.Digit('3'); calculator.Unary(UnaryOperation.Square);
        calculator.Digit('2'); calculator.Equals(); Assert.Equal(7, calculator.Value);
    }
    [Fact] public void ChainingUsesATransformedOperand()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Add); calculator.Digit('3'); calculator.Unary(UnaryOperation.Square);
        calculator.Operator(BinaryOperation.Multiply); calculator.Digit('2'); calculator.Equals(); Assert.Equal(28, calculator.Value);
    }
    [Theory]
    [InlineData(UnaryOperation.Square, "3", "9")]
    [InlineData(UnaryOperation.Reciprocal, "4", ".25")]
    [InlineData(UnaryOperation.SquareRoot, "4", "2")]
    [InlineData(UnaryOperation.Negate, "4", "-4")]
    public void UnaryOperations(UnaryOperation operation, string value, string expected)
    {
        var calculator = Enter(value); calculator.Unary(operation); Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), calculator.Value);
    }
    [Fact] public void SquareRootRetainsDecimalPrecision()
    {
        var calculator = Enter("2"); calculator.Unary(UnaryOperation.SquareRoot);
        Assert.Null(calculator.Error);
        Assert.Equal(100, calculator.ExactValue.SignificantDigits);
        Assert.StartsWith("1.41421356237309504880168872420969807856967187537694807317667973799", calculator.Entry);
    }
    [Fact] public void ErrorRecovery()
    {
        var calculator = Enter("5"); calculator.Operator(BinaryOperation.Divide); Type(calculator, "0"); calculator.Equals(); Assert.NotNull(calculator.Error);
        calculator.Digit('7'); Assert.Null(calculator.Error); Assert.Equal(7, calculator.Value); Assert.Empty(calculator.Expression);
    }
    [Fact] public void InvalidRootAndValuesBeyondDecimalRange()
    {
        var calculator = Enter("1"); calculator.Unary(UnaryOperation.Negate); calculator.Unary(UnaryOperation.SquareRoot); Assert.NotNull(calculator.Error);
        calculator.Clear(); calculator.SetValue(decimal.MaxValue); calculator.Operator(BinaryOperation.Add); calculator.Digit('1'); calculator.Equals(); Assert.Null(calculator.Error);
        Assert.Equal("79228162514264337593543950336", calculator.Entry);
    }
    [Fact] public void TinyDivisionRetainsPrecision()
    {
        var calculator = new StandardCalculator(); calculator.SetValue(.0000000000000000000000000001m);
        calculator.Operator(BinaryOperation.Divide); calculator.Digit('9'); calculator.Equals(); Assert.Null(calculator.Error);
        Assert.Equal(100, calculator.ExactValue.SignificantDigits); Assert.True(calculator.ExactValue.Sign > 0);
    }
    [Fact] public void ReciprocalBeyondDecimalScaleRetainsPrecision()
    {
        var calculator = new StandardCalculator(); calculator.SetValue(decimal.MaxValue); calculator.Unary(UnaryOperation.Reciprocal); Assert.Null(calculator.Error);
        Assert.Equal(100, calculator.ExactValue.SignificantDigits);
    }
    [Fact] public void RecalledTinyValuesRemainEditable()
    {
        var calculator = new StandardCalculator(); calculator.SetValue(.0000000000000000000000000001m);
        Assert.DoesNotContain("E", calculator.Entry); calculator.Backspace(); Assert.Equal(0, calculator.Value);
        calculator.DecimalPoint(); Assert.Equal(0, calculator.Value);
    }
    [Fact] public void OneHistoryEventPerCompletedOperation()
    {
        var calculator = Enter("2"); var count = 0; calculator.CalculationCompleted += _ => count++;
        calculator.Operator(BinaryOperation.Add); calculator.Digit('3'); calculator.Equals(); Assert.Equal(1, count);
        calculator.Equals(); Assert.Equal(2, count); calculator.Clear(); calculator.Equals(); Assert.Equal(2, count);
    }
    [Fact] public void ExcessiveInputLeavesPreviousValue()
    {
        var calculator = Enter(new string('9', 100)); var previous = calculator.ExactValue;
        Assert.Throws<CalculationException>(() => calculator.Digit('9')); Assert.Equal(previous, calculator.ExactValue);
    }
    [Fact] public void MemorySlotsAndArithmetic()
    {
        var memory = new MemoryBank(); Assert.Equal(0, memory.Recall()); memory.Store(12); memory.Add(3); memory.Add(-2);
        Assert.Equal(13, memory.Recall()); memory.Store(9); Assert.Equal(2, memory.Items.Count);
        memory.Remove(memory.Items[0].Id); Assert.Equal(13, memory.Recall()); memory.Clear(); Assert.Empty(memory.Items);
    }
    [Fact] public void MemoryRangeFailureIsAtomic()
    {
        var memory = new MemoryBank(); var maximum = BigDecimal.Parse("9e1000"); memory.Store(maximum);
        Assert.Throws<CalculationException>(() => memory.Add(maximum)); Assert.Equal(maximum, memory.RecallExact());
    }
}
