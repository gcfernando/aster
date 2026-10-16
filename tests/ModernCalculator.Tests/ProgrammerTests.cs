using System.Numerics;
using ModernCalculator.Core.Calculations;
using ModernCalculator.Core.Modes;
using Xunit;

namespace ModernCalculator.Tests;

public sealed class ProgrammerTests
{
    [Fact] public void BaseConversion()
    {
        var calculator = new ProgrammerCalculator(); calculator.Parse("FF", 16);
        Assert.Equal("255", calculator.Format(10)); Assert.Equal("11111111", calculator.Format(2)); Assert.Equal("377", calculator.Format(8));
    }
    [Fact] public void ValuesOneThrough100AgreeAcrossAllBasesAndWidths()
    {
        foreach (var width in new[] { 8, 16, 32, 64 })
        for (var number = 1; number <= 100; number++)
        {
            var calculator = new ProgrammerCalculator(); calculator.SetWidth(width); calculator.SetValue(number);
            foreach (var radix in new[] { 2, 8, 10, 16 })
                Assert.Equal(Convert.ToString(number, radix).ToUpperInvariant(), calculator.Format(radix));
        }
    }
    [Theory] [InlineData(8)] [InlineData(16)] [InlineData(32)] [InlineData(64)]
    public void SignedAndUnsignedBoundaries(int width)
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(width); calculator.SetValue((BigInteger.One << width) - 1);
        Assert.Equal(-BigInteger.One, calculator.Value); calculator.Signed = false;
        Assert.Equal((BigInteger.One << width) - 1, calculator.Value);
        calculator.Apply(IntegerOperation.Add, 1); Assert.Equal(BigInteger.Zero, calculator.Value);
    }
    [Theory]
    [InlineData(IntegerOperation.And, 12, 10, 8)] [InlineData(IntegerOperation.Or, 12, 10, 14)]
    [InlineData(IntegerOperation.Xor, 12, 10, 6)] [InlineData(IntegerOperation.LeftShift, 3, 2, 12)]
    [InlineData(IntegerOperation.RightShift, 16, 2, 4)] [InlineData(IntegerOperation.Add, 127, 1, -128)]
    [InlineData(IntegerOperation.Subtract, -128, 1, 127)] [InlineData(IntegerOperation.Multiply, 100, 3, 44)]
    [InlineData(IntegerOperation.Divide, -7, 3, -2)] [InlineData(IntegerOperation.Remainder, -7, 3, -1)]
    [InlineData(IntegerOperation.Divide, -128, -1, -128)]
    public void Operations(IntegerOperation operation, int left, int right, int expected)
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(8); calculator.SetValue(left); calculator.Apply(operation, right); Assert.Equal(new BigInteger(expected), calculator.Value);
    }
    [Fact] public void RightShiftDependsOnSignedness()
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(8); calculator.SetValue(-2); calculator.Apply(IntegerOperation.RightShift, 1); Assert.Equal(-BigInteger.One, calculator.Value);
        calculator.SetValue(-2); calculator.Signed = false; calculator.Apply(IntegerOperation.RightShift, 1); Assert.Equal(new BigInteger(127), calculator.Value);
    }
    [Fact] public void WideningAndNarrowing()
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(8); calculator.SetValue(-1); calculator.SetWidth(16); Assert.Equal("FFFF", calculator.Format(16));
        calculator.Signed = false; calculator.SetWidth(8); calculator.SetValue(255); calculator.SetWidth(16); Assert.Equal(new BigInteger(255), calculator.Value);
    }
    [Fact] public void NotAndBitToggle()
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(8); calculator.Not(); Assert.Equal(-BigInteger.One, calculator.Value);
        calculator.ToggleBit(7); Assert.Equal(new BigInteger(127), calculator.Value);
    }
    [Theory] [InlineData(-1)] [InlineData(8)] [InlineData(256)]
    public void InvalidShiftIsAtomic(int count)
    {
        var calculator = new ProgrammerCalculator(); calculator.SetWidth(8); calculator.SetValue(15);
        Assert.Throws<CalculationException>(() => calculator.Apply(IntegerOperation.LeftShift, count)); Assert.Equal(new BigInteger(15), calculator.Value);
    }
    [Theory] [InlineData("2", 2)] [InlineData("G", 16)] [InlineData("8", 8)] [InlineData("1.5", 10)] [InlineData("-", 10)]
    public void RejectsInvalidInput(string text, int numberBase) => Assert.Throws<CalculationException>(() => new ProgrammerCalculator().Parse(text, numberBase));
    [Fact] public void FullUnsigned64BitValue()
    {
        var calculator = new ProgrammerCalculator { Signed = false }; calculator.Parse("18446744073709551615", 10);
        Assert.Equal("FFFFFFFFFFFFFFFF", calculator.Format(16)); Assert.Equal("18446744073709551615", calculator.Format(10));
    }
    [Fact] public void ReusedDecimalResultsPreserveValueInAnyDisplayBase()
    {
        var calculator = new ProgrammerCalculator(); calculator.ParseDecimalResult("255"); Assert.Equal("FF", calculator.Format(16));
        calculator.ParseDecimalResult("2E3"); Assert.Equal(new BigInteger(2000), calculator.Value);
        Assert.Throws<CalculationException>(() => calculator.ParseDecimalResult("1.5")); Assert.Equal(new BigInteger(2000), calculator.Value);
    }
    [Fact] public void DivideByZeroIsAtomic()
    {
        var calculator = new ProgrammerCalculator(); calculator.SetValue(6); Assert.Throws<CalculationException>(() => calculator.Apply(IntegerOperation.Divide, 0)); Assert.Equal(new BigInteger(6), calculator.Value);
    }
}
